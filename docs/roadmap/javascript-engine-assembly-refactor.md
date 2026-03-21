# Roadmap: Refactor Broiler.JavaScript Using Separate Assemblies

This document describes a fresh roadmap for refactoring the Broiler.JavaScript
engine (forked from YantraJS) into separate assemblies. It focuses exclusively
on **logical separation** and **assembly structure** to improve maintainability,
scalability, and testability.

---

## 1. Current State

### 1.1 Project Inventory

| Project | Assembly Name | Role | Status |
|---------|---------------|------|--------|
| `Broiler.JavaScript.Core` | Broiler.JavaScript.Core | Engine core: compiler, runtime, built-in objects, module system | Active — being decomposed |
| `Broiler.JavaScript.Ast` | Broiler.JavaScript.Ast | AST node types, shared primitives (`FastToken`, `StringSpan`, `FastNodeType`, etc.) | ✅ Extracted (Phase 1) |
| `Broiler.JavaScript.Parser` | Broiler.JavaScript.Parser | Lexer (`FastScanner`), recursive-descent parser (`FastParser`), scope tracking | ✅ Extracted (Phase 2) |
| `Broiler.JavaScript.Storage` | Broiler.JavaScript.Storage | Property hash maps, virtual memory, concurrent caches, `JSPropertyAttributes`, `JSProperty`, `PropertySequence`, `ElementArray`, `KeyString`/`KeyStrings` | ✅ Extracted (Phase 3); all storage types moved from Core |
| `Broiler.JavaScript.Debugger` | Broiler.JavaScript.Debugger | V8 Inspector Protocol handler, protocol data types | ✅ Extracted (Phase 4, partial); `InternalsVisibleTo` bridge removed |
| `Broiler.JavaScript.Clr` | Broiler.JavaScript.Clr | .NET ↔ JavaScript type bridging (`ClrProxy`, `ClrType`, `DefaultClrInterop`) | ✅ Extracted (Phase 5) |
| `Broiler.JavaScript.BuiltIns` | Broiler.JavaScript.BuiltIns | Extracted built-in objects (WeakRef, FinalizationRegistry, EventTarget, Event) | ✅ Extracted (Phase 6, partial) |
| `Broiler.JavaScript.Compiler` | Broiler.JavaScript.Compiler | AST → LINQ Expression Tree compilation (`FastCompiler`, 40+ partial files) | ✅ Extracted (Phase 7) |
| `Broiler.JavaScript.Modules` | Broiler.JavaScript.Modules | ES module system (`JSModuleContext`, `JSModule`, `ModuleCache`) | ✅ Extracted (Phase 8) |
| `Broiler.JavaScript.Runtime` | Broiler.JavaScript.Runtime | Core value type system (`JSValue`, `Arguments`, `PropertyKey`, `JSFunctionDelegate`, `IElementEnumerator`), interface abstractions (`IJSPrototype`, `IJSSymbol`), runtime contracts (`IJSModuleResolver`, `ExportAttribute`, `DefaultExportAttribute`), utility types (`CancellableDisposableAction`, `ObjectStatus`, `StringExtensions`) | ✅ Phase 9b complete — core value types moved from Core |
| `Broiler.JavaScript.ExpressionCompiler` | Broiler.JavaScript.ExpressionCompiler | LINQ Expression Tree → IL compilation | Pre-existing |
| `Broiler.JavaScript.JSClassGenerator` | Broiler.JavaScript.JSClassGenerator | Roslyn source generator for C#-to-JS bindings | Pre-existing |
| `Broiler.JavaScript.Network` | YantraJS.Network | Fetch API / network module | ✅ Updated (references, TFM, namespaces aligned) |
| `Broiler.JavaScript.ModuleExtensions` | (library) | Fluent module-registration extensions | ✅ Complete — references, TFM, namespaces aligned; bugs fixed; dedicated test project added |
| `Broiler.JavaScript.NodePollyfill` | YantraJS.NodePollyfill | Node.js compatibility polyfills | ✅ Updated (references, TFM, namespaces aligned) |
| `Broiler.JavaScript` | YantraJS (exe) | CLI REPL / runner | Pre-existing |
| `Broiler.JavaScript.Core.Tests` | (test) | Unit tests for the core engine (641 tests) | Active |
| `Broiler.JavaScript.Ast.Tests` | (test) | Unit tests for Ast assembly (73 tests) | ✅ Created |
| `Broiler.JavaScript.Parser.Tests` | (test) | Unit tests for Parser assembly (78 tests) | ✅ Created |
| `Broiler.JavaScript.Storage.Tests` | (test) | Unit tests for Storage assembly (100 tests) | ✅ Created |
| `Broiler.JavaScript.Debugger.Tests` | (test) | Unit tests for Debugger assembly (23 tests) | ✅ Created |
| `Broiler.JavaScript.Clr.Tests` | (test) | Unit tests for Clr assembly (29 tests) | ✅ Created |
| `Broiler.JavaScript.BuiltIns.Tests` | (test) | Unit tests for BuiltIns assembly (16 tests) | ✅ Created |
| `Broiler.JavaScript.Compiler.Tests` | (test) | Unit tests for Compiler assembly (9 tests) | ✅ Created |
| `Broiler.JavaScript.Modules.Tests` | (test) | Unit tests for Modules assembly (9 tests) | ✅ Created |
| `Broiler.JavaScript.Runtime.Tests` | (test) | Unit tests for Runtime assembly (20 tests) | ✅ Created |

### 1.2 Problem

`Broiler.JavaScript.Core` is a monolithic assembly containing **615+ source
files** across 10 distinct subsystems. All subsystems share a single namespace
root (`Broiler.JavaScript.Core`) and compile into a single DLL. This creates
several problems:

- **Tight coupling.** Types like `JSContext`, `JSValue`, and `Arguments` are
  referenced by nearly every file, making it difficult to modify one subsystem
  without risk of breaking another.
- **Large compilation unit.** Every change recompiles the entire assembly even
  when only one subsystem is affected.
- **Testing friction.** Internal subsystem boundaries are invisible to the test
  framework; unit tests cannot target a subsystem in isolation.
- **Consumer overhead.** Downstream projects (e.g., `Broiler.App`,
  `Broiler.Cli`) that need only script evaluation still pull in the debugger,
  CLR interop, and all built-in objects.

### 1.3 Compilation Pipeline

```
JavaScript source
      ↓
  FastParser (lexer + recursive-descent parser) → AST nodes
      ↓
  FastCompiler + LinqExpressions → LINQ Expression Trees
      ↓
  ExpressionCompiler → IL / DynamicMethod
      ↓
  CLR execution via JSContext + JSValue interop
```

---

## 2. Target Assembly Structure

The refactored engine splits `Broiler.JavaScript.Core` into the following
assemblies. Each assembly has a single, well-defined responsibility.

### 2.1 Assembly Map

| # | Assembly | Source | Responsibility |
|---|----------|--------|----------------|
| 1 | **Broiler.JavaScript.Ast** | `FastParser/Ast/` | AST node type definitions (data-only, no logic) |
| 2 | **Broiler.JavaScript.Parser** | `FastParser/` (minus `Compiler/`) | Lexer (`FastScanner`), recursive-descent parser, scope tracking |
| 3 | **Broiler.JavaScript.Compiler** | `FastParser/Compiler/`, `LinqExpressions/`, `CodeGen/`, `LambdaGen/` | AST → LINQ Expression Tree compilation, generator rewriting |
| 4 | **Broiler.JavaScript.Runtime** | `Core/` top-level: `JSContext`, `JSValue`, `JSVariable`, `Arguments`, `Bootstrap`, `KeyString`, `CoreScript`, `Emit/` | Execution context, value type system, property key interning, IL emission helpers |
| 5 | **Broiler.JavaScript.Storage** | `Core/Storage/` | Property hash maps and internal storage used by `JSObject` |
| 6 | **Broiler.JavaScript.BuiltIns** | `Core/{Array,BigInt,Boolean,Class,DataView,Date,Decimal,Disposable,Error,Events,Function,Generator,Global,Intl,Iterator,Json,Map,Number,Object,Objects,Primitive,Promise,Proxy,RegExp,Set,String,Symbol,Weak}/` | All ECMAScript built-in object implementations |
| 7 | **Broiler.JavaScript.Clr** | `Core/Clr/` | .NET ↔ JavaScript type bridging (`ClrProxy`, `ClrType`, `IClrInterop`) |
| 8 | **Broiler.JavaScript.Modules** | `Core/Module/` | ES module loading, `import`/`export` resolution |
| 9 | **Broiler.JavaScript.Debugger** | `Debugger/`, `Core/Debug/` | V8 Inspector protocol, `IDebugger` contract |
| 10 | **Broiler.JavaScript.ExpressionCompiler** | *(already separate)* | LINQ Expression Tree → IL compilation (unchanged) |
| 11 | **Broiler.JavaScript.JSClassGenerator** | *(already separate)* | Roslyn source generator (unchanged) |
| 12 | **Broiler.JavaScript.ModuleExtensions** | `ModuleExtensions/` | Fluent module-registration extensions (`ModuleBuilder`, `JSModuleContextExtension`) |
| 13 | **Broiler.JavaScript.All** | *(meta-package)* | Convenience reference that transitively includes all engine assemblies |

### 2.2 Assembly Dependency Graph

```
                    ┌──────────────────┐
                    │  Broiler.JS.Ast  │  (no dependencies)
                    └────────┬─────────┘
                             │
                    ┌────────▼─────────┐
                    │ Broiler.JS.Parser│
                    └────────┬─────────┘
                             │
              ┌──────────────▼──────────────┐
              │    Broiler.JS.Compiler      │
              │  (depends on Ast, Runtime,  │
              │   ExpressionCompiler)       │
              └──────────────┬──────────────┘
                             │
      ┌──────────┐  ┌───────▼────────┐  ┌──────────────┐
      │ Storage  │◄─│Broiler.JS      │──►│   IL Emit    │
      └──────────┘  │  .Runtime      │  │  (in Runtime) │
                    └──┬──┬──┬───────┘  └──────────────┘
                       │  │  │
           ┌───────────┘  │  └───────────┐
           ▼              ▼              ▼
  ┌────────────────┐ ┌──────────┐ ┌──────────────┐
  │ Broiler.JS     │ │Broiler.JS│ │ Broiler.JS   │
  │  .BuiltIns     │ │  .Clr    │ │  .Debugger   │
  └────────┬───────┘ └──────────┘ └──────────────┘
           │
  ┌────────▼───────┐
  │ Broiler.JS     │
  │  .Modules      │
  └────────────────┘
```

### 2.3 Dependency Matrix

| Assembly | Depends On |
|----------|------------|
| **Ast** | *(none)* |
| **Parser** | Ast |
| **Compiler** | Ast, Runtime (interfaces only), ExpressionCompiler |
| **Runtime** | Ast (for `CoreScript`), Storage |
| **Storage** | Runtime (interfaces: `KeyString`, `JSValue` base) |
| **BuiltIns** | Runtime |
| **Clr** | Runtime |
| **Modules** | Runtime, Parser (for source compilation), Clr (for `ClrModule`) |
| **Debugger** | Runtime (via `IDebugger` interface) |
| **ExpressionCompiler** | *(none — already separate)* |

> **Circular dependency note:** Runtime depends on Storage and vice-versa.
> Resolution: extract shared types (`KeyString`, base `JSValue` property
> contracts) into **Broiler.JavaScript.Ast** or a thin
> **Broiler.JavaScript.Primitives** assembly if needed, so both Runtime and
> Storage depend downward on the same foundation.

---

## 3. Assembly Details

### 3.1 Broiler.JavaScript.Ast

**Purpose:** Pure data definitions for the abstract syntax tree. No behaviour,
no references to runtime types.

**Public API:**
- `AstNode` base class and all concrete node types (`AstBinaryExpression`,
  `AstFunctionDeclaration`, `AstProgram`, etc.)
- `FastNodeType` enum
- `SpanLocation` (source location tracking)

**Design Rules:**
- No dependency on any other Broiler.JavaScript assembly.
- All node types are read-only (immutable after construction).
- `StringSpan` (currently in Core root) moves here as a shared primitive.

---

### 3.2 Broiler.JavaScript.Parser

**Purpose:** Tokenize JavaScript source code and produce an AST.

**Public API:**
- `FastParser.ParseProgram(StringSpan source) → AstProgram`
- `FastTokenStream` (for consumers that want raw tokens)
- `FastToken`, `TokenTypes`

**Key Types:**
- `FastScanner` (lexer)
- `FastParser` (30+ partial-class files)
- `FastScope`, `FastScopeItem`
- `FastKeywords`, `FastKeywordMap`

**Design Rules:**
- Depends only on **Ast**.
- No references to `JSContext`, `JSValue`, or any runtime type.

---

### 3.3 Broiler.JavaScript.Compiler

**Purpose:** Transform an AST into executable LINQ Expression Trees (which are
later compiled to IL by ExpressionCompiler).

**Public API:**
- `FastCompiler(StringSpan code)` → constructor parses + compiles
- `FastCompiler.Method` → `JSFunctionDelegate`
- Expression builder types (for advanced consumers)

**Key Types:**
- `FastCompiler` (30+ partial-class files in `Compiler/`)
- `JS*Builder` expression builders (40+ files in `LinqExpressions/`)
- `GeneratorsV2/` generator function rewriting (12 files)
- `CodeGen/` and `LambdaGen/` helpers

**Design Rules:**
- Depends on **Ast** (reads AST nodes), **Runtime** (references `JSValue`,
  `JSContext`, `Arguments` via interfaces where possible), and
  **ExpressionCompiler** (for final IL emission).
- The compiler references runtime types because the expression trees it builds
  contain calls to runtime methods. This coupling is inherent to the
  compilation model but should use interfaces and well-known method references
  rather than reaching into runtime internals.

---

### 3.4 Broiler.JavaScript.Runtime

**Purpose:** Core value type system and cross-cutting contracts — the base types
and interfaces every other assembly needs to interact with JavaScript values.

**Current Contents (Phase 9d — CoreScript extracted, all contract interfaces complete):**
- `JSValue` — base class for all JavaScript values (arithmetic, comparison, property access)
- `Arguments` — function invocation argument struct (spread support, rest parameters)
- `PropertyKey` — union of string key (`KeyString`) and symbol key (`IJSSymbol`)
- `JSFunctionDelegate` — delegate type for JavaScript function invocation
- `IElementEnumerator` — interface for array/spread enumeration
- `IJSPrototype` — interface abstracting the prototype chain
- `IJSSymbol` — interface abstracting JavaScript Symbol values
- `IJSContext` — context abstraction with `CodeCache` and `WaitTask` properties
- `IJSFunction` — interface abstracting JavaScript function invocation
- `ObjectStatus` — flags enum for object extensibility/freeze/seal state
- `IJSModuleResolver` — ES module resolution contract
- `ExportAttribute`, `DefaultExportAttribute` — module export markers
- `CancellableDisposableAction` — cancellable `IDisposable` utility
- `StringExtensions` — string comparison helpers
- `IDebugger` — debugger notification contract
- `IClrInterop` — CLR ↔ JS marshalling contract
- `IJSCompiler` — source → expression tree compilation contract
- `ICodeCache` — compiled code caching contract
- `JSCode` — code unit struct (location + source + compiler)
- `JSCodeCompiler` — delegate for deferred compilation
- `IBuiltInRegistry` — built-in registration contract
- `CoreScript` — high-level compile-and-evaluate API (factory-delegate-based)

**Future Contents (deferred — see Section 26 for architectural assessment, Section 29.5 for action plan):**
- `JSObject`, `JSFunction`, `JSContext`, `JSPrototype` — extraction deferred;
  concrete types intentionally remain in Core. Recommended via intermediate
  `Broiler.JavaScript.ObjectModel` assembly if pursued in the future.
  Preconditions: P1 coverage targets, P2 integration tests, P2 BuiltIns
  proof-of-concept must be completed first.

**Design Rules:**
- Depends on **Ast** (for `IPropertyValue`/`IPropertyAccessor`, `StringSpan`),
  **Storage** (for `KeyString`, `JSProperty`, `PropertySequence`, `ElementArray`),
  and **ExpressionCompiler** (for `YExpression<T>`).
- Uses factory delegates (`CreateNumber`, `CreateString`, etc.) wired via
  `[ModuleInitializer]` in Core to avoid circular dependency.
- `Bootstrap` must become pluggable: instead of hard-coding every built-in
  type, it calls `IBuiltInRegistry.Register(JSContext)` so that `BuiltIns`,
  `Clr`, and `Modules` assemblies register themselves.

---

### 3.5 Broiler.JavaScript.Storage

**Purpose:** Internal hash maps and property storage used by `JSObject` for
fast property lookup.

**Public API:**
- `SAUint32Map<T>`, `PropertyMap`, and other storage data structures.

**Design Rules:**
- Depends on shared primitives (`KeyString`, `JSValue` property interfaces).
- If a circular dependency with Runtime arises, extract the shared contracts
  into **Ast** or a new **Primitives** assembly.

---

### 3.6 Broiler.JavaScript.BuiltIns

**Purpose:** All ECMAScript standard library objects — Array, String, Number,
Date, Promise, Map, Set, RegExp, JSON, Math, Symbol, Error, Proxy, Reflect,
Intl, Generator, BigInt, DataView, WeakRef, etc.

**Public API:**
- Each built-in type class (e.g., `JSArray`, `JSString`, `JSNumber`)
- A module-level `BuiltInRegistry : IBuiltInRegistry` that registers all
  built-in types on a `JSContext` during bootstrap.

**Design Rules:**
- Depends on **Runtime** only.
- Built-in types reference each other only where the ECMAScript spec requires
  it (e.g., `Array` uses `Number` for index coercion). Minimize gratuitous
  cross-references.
- Each built-in type folder is a self-contained unit; future extraction into
  per-type assemblies (e.g., `BuiltIns.Promise`) is possible but not planned
  for this phase. See Section 29.2 for the next batch of BuiltIns extraction
  via factory delegate and interface abstraction patterns.

---

### 3.7 Broiler.JavaScript.Clr

**Purpose:** .NET ↔ JavaScript type bridging, allowing .NET objects to be used
from JavaScript and vice versa.

**Public API:**
- `ClrProxy` — wraps a .NET object as a `JSValue`
- `ClrType`, `ClrTypeBuilder` — reflection-based type bridging
- `ClrModule` — exposes a .NET assembly as a JS module
- `DefaultClrInterop : IClrInterop` — default implementation

**Design Rules:**
- Depends on **Runtime** only.
- Implements `IClrInterop` (defined in Runtime).
- Registers itself via `IBuiltInRegistry` so that `JSContext` does not need a
  hard reference to this assembly.

---

### 3.8 Broiler.JavaScript.Modules

**Purpose:** ES module system — `import`/`export` resolution, module caching,
module context.

**Public API:**
- `JSModuleContext` — module-aware execution context
- `JSModule` — single module representation
- Module resolution and caching

**Design Rules:**
- Depends on **Runtime**, **Parser** (to compile module source), and **Clr**
  (for `ClrModule`).
- The dependency on Clr is optional and can be made conditional via
  `IClrInterop`.

---

### 3.9 Broiler.JavaScript.Debugger

**Purpose:** V8 Inspector Protocol implementation for Chrome DevTools
integration.

**Public API:**
- `V8InspectorProtocol` — protocol handler
- `V8Debugger`, `V8Runtime`, etc.
- Debug hooks (`JSConsole`)

**Design Rules:**
- Depends on **Runtime** only (via `IDebugger` interface).
- Must not reference Parser, Compiler, or BuiltIns directly.
- Source-map support may require a read-only reference to AST source locations.

---

## 4. Migration Strategy

### 4.1 Guiding Principles

1. **One assembly at a time.** Extract the least-coupled subsystem first. The
   existing test suite must pass after each extraction.
2. **Interface-first.** Before moving code, define the cross-assembly interface.
   The monolith implements the interface inline, then the extracted assembly
   takes over.
3. **InternalsVisibleTo as a bridge.** During migration, the new assembly may
   temporarily use `InternalsVisibleTo` to access Core internals. These
   must be converted to public API before the milestone is considered complete.
4. **Namespace continuity.** Namespaces change to match the new assembly name
   (e.g., `Broiler.JavaScript.Parser` instead of
   `Broiler.JavaScript.Core.FastParser`). Use `global using` aliases and
   `[assembly: TypeForwardedTo]` attributes during migration to avoid breaking
   downstream consumers.

### 4.2 Extraction Order

Assemblies are extracted in order of increasing coupling to minimize cascading
changes:

| Phase | Assembly | Rationale | Status |
|-------|----------|-----------|--------|
| **Phase 1** | **Ast** | Zero dependencies. Pure data types. Smallest, safest extraction. | ✅ Complete; test project created (73 tests) |
| **Phase 2** | **Parser** | Depends only on Ast. Self-contained lexer + parser. | ✅ Complete; test project created (78 tests) |
| **Phase 3** | **Storage** | Depends on shared primitives. Decouples property storage from runtime logic. | ✅ Complete — pure storage types + `JSPropertyAttributes` + `JSProperty` (interface-typed fields) + `PropertySequence` + `ElementArray` + `KeyString`/`KeyStrings`/`KeyType` extracted; test project created (100 tests) |
| **Phase 4** | **Debugger** | Already behind `IDebugger` interface. Largely independent. | ✅ Partial — V8 Inspector Protocol extracted; test project created (23 tests); `InternalsVisibleTo` bridge removed (all accessed APIs now public) |
| **Phase 5** | **Clr** | Already behind `IClrInterop` interface. Medium coupling. | ✅ Complete — 11 files extracted; ClrProxyBuilder decoupled via delegate pattern; FallbackClrInterop as Core default; test project created (29 tests) |
| **Phase 6** | **BuiltIns** | High coupling to Runtime, but only through `JSValue`/`JSContext`. Requires `IBuiltInRegistry` to be in place. | ✅ Partial — WeakRef, FinalizationRegistry, EventTarget, Event, CustomEvent, DomEventHandler extracted; test project created (16 tests); `AdditionalRegistrations` delegate added to `DefaultBuiltInRegistry`; module initializer pattern |
| **Phase 7** | **Compiler** | Depends on Ast, Runtime, and ExpressionCompiler. Requires stable interfaces. | ✅ Complete; test project created (9 tests) |
| **Phase 8** | **Modules** | Last — depends on Runtime, Parser, and Clr. | ✅ Complete — extracted; `IJSModuleResolver` moved to Runtime assembly |

### 4.3 Per-Phase Workflow

Each extraction phase follows the same steps:

1. **Define interface.** Create the public API contract in the target assembly's
   project (initially empty).
2. **Move source files.** Relocate `.cs` files from `Broiler.JavaScript.Core`
   to the new project. Update namespaces.
3. **Add project reference.** `Broiler.JavaScript.Core` adds a
   `<ProjectReference>` to the new assembly and re-exports its types via
   `global using` during transition.
4. **Fix compilation errors.** Resolve missing references by adding necessary
   `using` directives or introducing interface abstractions.
5. **Run tests.** All existing tests in `Broiler.JavaScript.Core.Tests` must
   pass without modification.
6. **Add assembly-specific tests.** Create a new test project (e.g.,
   `Broiler.JavaScript.Parser.Tests`) exercising the extracted assembly's
   public API in isolation.
7. **Remove `InternalsVisibleTo` bridges.** Any temporary internal access must
   be replaced with public API before the phase is marked complete.
8. **Update solution files.** Add the new project to `Broiler.slnx` and
   `YantraJS.sln`.

---

## 5. Timeline and Milestones

### Milestone 1 — Foundation (Phases 1–2)

**Phase 1 — Ast: ✅ Complete (2026-03-19)**

**Phase 2 — Parser: ✅ Complete (2026-03-19)**

**Deliverables:**
- `Broiler.JavaScript.Ast` assembly with all AST node types ✅
- `Broiler.JavaScript.Parser` assembly with lexer and parser ✅
- `Broiler.JavaScript.Ast.Tests` project — 73 assembly-specific tests ✅ (2026-03-20)
- `Broiler.JavaScript.Parser.Tests` project — 78 assembly-specific tests ✅ (2026-03-20)
- All existing tests pass ✅

**Key Metrics:**
- Ast compiles with **zero** references to Runtime types. ✅
- `Broiler.JavaScript.Core` references Ast but not vice versa. ✅
- Parser compiles with **zero** references to Runtime types. ✅
- Parser depends only on Ast and ExpressionCompiler (shared primitives). ✅

### Milestone 2 — Infrastructure Extraction (Phases 3–4)

**Phase 3 — Storage: ✅ Complete (2026-03-21)**

**Phase 4 — Debugger: ✅ Partial (2026-03-19)**

**Deliverables:**
- `Broiler.JavaScript.Storage` assembly ✅ (pure storage types)
- `Broiler.JavaScript.Debugger` assembly ✅ (V8 Inspector Protocol)
- `Broiler.JavaScript.Storage.Tests` — 100 assembly-specific tests ✅ (2026-03-21)
- `Broiler.JavaScript.Debugger.Tests` — 23 assembly-specific tests ✅ (2026-03-20)

**Key Metrics:**
- Storage has no reference to `JSContext` (only to Ast shared primitives). ✅
- Debugger accesses Core via public APIs only (no `InternalsVisibleTo` needed). ✅
- Storage.Tests references only Storage (no Core dependency). ✅
- ~~3 runtime-dependent storage types (JSProperty, PropertySequence, ElementArray)
  remain in Core until Runtime extraction resolves the circular dependency.~~
  ✅ All three types moved to Storage. `JSProperty` uses interface-typed fields
  (`IPropertyValue`, `IPropertyAccessor`). `PropertySequence` and `ElementArray`
  use interface-typed method parameters. `PropertyValueEnumerator` (formerly
  `PropertySequence.ValueEnumerator`) remains in Core (depends on `JSObject`).

### Milestone 3 — Interop Extraction (Phases 5–6)

**Status:** Phase 5 ✅ complete; Phase 6 ✅ complete (12 types extracted to BuiltIns; remaining candidates assessed as impractical — see Section 31)

**Deliverables:**
- `Broiler.JavaScript.Clr` assembly ✅
- `Broiler.JavaScript.Clr.Tests` — 29 assembly-specific tests ✅
- `Broiler.JavaScript.BuiltIns` assembly ✅ (partial — WeakRef, FinalizationRegistry, EventTarget, Event, CustomEvent, DomEventHandler)
- `Broiler.JavaScript.BuiltIns.Tests` — 16 assembly-specific tests ✅
- `IBuiltInRegistry` pluggable bootstrap in Runtime ✅
- `DefaultBuiltInRegistry.AdditionalRegistrations` delegate for satellite assembly registration ✅

**Prerequisites (see Phase 5–8 analysis in Implementation Log):**
1. ~~Refactor Core to use `IClrInterop` exclusively~~ — ✅ Done.
2. ~~Implement `IBuiltInRegistry` pluggable bootstrap in Core~~ — ✅ Done.
3. ~~Configure `JSClassGenerator` to work with extracted assemblies~~ — ✅ Done
   (Clr import removed from generated code; multi-assembly generation verified
   via Network assembly pattern; each assembly needs its own `Names` class with
   `[JSRegistrationGenerator]`).

**Remaining Phase 6 work (P2):**
- [x] Extract `JSDisposableStack` + `JSSuppressedError` via factory delegates
  (Section 29.2). ✅ Completed 2026-03-21 — see Section 31.
- [x] Extract `JSDecimal` via factory delegates (Section 29.2).
  ✅ Completed 2026-03-21 — see Section 31.
- [x] Extract `JSIntl` family via factory delegates (Section 29.2).
  ✅ Completed 2026-03-21 — see Section 31.
- [x] Assess remaining candidates (JSArray, JSString, etc.) after initial
  extractions prove the pattern at scale. ✅ Assessed 2026-03-21 — see
  Section 31 for analysis; remaining types impractical to extract.

**Key Metrics:**
- `Broiler.JavaScript.Clr` compiles independently with zero errors. ✅
- `JSContext` bootstrap is driven entirely by `IBuiltInRegistry`. ✅
- CLR interop is pluggable via `IClrInterop` interface. ✅
- Removing the Clr assembly from the dependency chain produces a functional
  (but feature-reduced) runtime using `FallbackClrInterop`. ✅

### Milestone 4 — Compiler and Modules (Phases 7–8)

**Status:** ✅ Complete — Compiler and Modules extracted

**Deliverables:**
- `Broiler.JavaScript.Compiler` assembly ✅
- `Broiler.JavaScript.Compiler.Tests` — 9 assembly-specific tests ✅
- `Broiler.JavaScript.Modules` assembly ✅
- `Broiler.JavaScript.Modules.Tests` — 9 assembly-specific tests ✅
- Full integration test suite verifying end-to-end script execution ✅ (verified via Core.Tests)

**Prerequisites (see Phase 5–8 analysis in Implementation Log):**
1. ~~Define stable Runtime interfaces for compiler consumption~~ — ✅ Done.
   `IJSCompiler` already exists in `FastParser/Compiler/IJSCompiler.cs` and is
   wired into `CoreScript.Compiler`. `DefaultJSCompiler` is the extractable
   implementation.
2. ~~Resolve `JSModuleContext extends JSContext` inheritance dependency~~ — ✅
   Confirmed: no circular dependency exists. `JSModuleContext` follows the
   upward-dependency pattern (Modules → Core). `IJSModuleResolver` interface
   defined for pluggable module resolution.
3. ~~Configure `JSClassGenerator` for multi-assembly namespace support~~ — ✅
   Done. Stale `using Broiler.JavaScript.Core.Core.Clr;` removed from generated
   code. Generator already supports multi-assembly via per-assembly `Names`
   class pattern.

**Key Metrics:**
- All 10 assemblies compile independently. ✅
- Existing `Broiler.JavaScript.Core.Tests` pass (641 tests). ✅
- `Broiler.JavaScript.Compiler.Tests` pass (9 tests). ✅
- `Broiler.JavaScript.Modules.Tests` pass (9 tests). ✅

### Milestone 5 — Runtime Extraction and `InternalsVisibleTo` Elimination (Phases 9–10)

**Status:** Phase 10 ✅ complete; Phase 9a ✅ complete; Phase 9b ✅ complete; Phase 9c ✅ complete; Phase 9c+ ✅ complete; Phase 9d ✅ complete; concrete type extraction deferred (see Sections 26, 29.5)

**Phase 9 — Runtime Extraction:**
- [x] Move `IJSModuleResolver` to Runtime. ✅ (2026-03-20)
- [x] Move `ExportAttribute` and `DefaultExportAttribute` to Runtime. ✅ (2026-03-20)
- [x] Move `CancellableDisposableAction` to Runtime. ✅ (2026-03-20)
- [x] Phase 9a: Move `JSProperty` to Storage with interface-typed fields
  (`IPropertyValue`/`IPropertyAccessor`). ✅ (2026-03-20)
- [x] Phase 9a: Move `PropertySequence`, `ElementArray` to Storage. ✅ (2026-03-21)
  Interface-typed method parameters (`IPropertyValue`, `IPropertyAccessor`).
  `PropertyValueEnumerator` extracted to Core. `PropertySequenceCoreExtensions`
  for `JSFunctionDelegate` overload and `TypeErrorFactory` initialization.
- [x] Phase 9a: Move `KeyString`/`KeyStrings`/`KeyType` to Storage. ✅ (2026-03-20)
- [x] Move `ObjectStatus` enum to Runtime. ✅ (2026-03-21)
- [x] Fix cross-platform `Path.Combine` in Runtime tests
  (`StubModuleResolver.Resolve` now normalizes separators). ✅ (2026-03-21)
- [x] Phase 9b: Move `JSValue`, `Arguments`, `PropertyKey`,
  `JSFunctionDelegate`, `IElementEnumerator` to Runtime. ✅ (2026-03-21)
- [x] Phase 9b: Create `IJSPrototype` and `IJSSymbol` interface abstractions
  in Runtime. ✅ (2026-03-21)
- [x] Phase 9b: Wire factory delegates (`InvokePropertyGetter`,
  `CreatePrototypeObject`) via `[ModuleInitializer]`. ✅ (2026-03-21)
- [x] Phase 9b: Add `TypeForwardedTo` for all moved types (7 new entries). ✅ (2026-03-21)
- [x] Phase 9d: Move `CoreScript` to Runtime via factory delegates. ✅ (2026-03-21)
- [x] Phase 9d: Extract `ExpressionHolder` to standalone file in Core. ✅ (2026-03-21)
- [x] Phase 9d: Create `IJSFunction` interface in Runtime. ✅ (2026-03-21)
- [x] Phase 9d: Extend `IJSContext` with `CodeCache` and `WaitTask`
  properties. ✅ (2026-03-21)
- [x] Phase 9d: `JSFunction` implements `IJSFunction`. ✅ (2026-03-21)
- [x] Phase 9d: Wire `CoreScript` factory delegates via `[ModuleInitializer]`
  in `CoreScriptCoreExtensions`. ✅ (2026-03-21)
- [ ] *(Deferred)* Move `JSObject`, `JSFunction`, `JSContext` concrete types
  to Runtime. ⏳ See Section 26 for architectural assessment; Section 29.5
  for action plan and preconditions.
- [x] Move remaining contract interfaces (`IBuiltInRegistry`, `IClrInterop`,
  `IDebugger`, `IJSCompiler`) to Runtime. ✅ `IBuiltInRegistry` unblocked via
  `IJSContext` interface abstraction (Phase 9c+, 2026-03-21).
- [x] Phase 9c+: Create `IJSContext` interface in Runtime. ✅ (2026-03-21)

**Phase 10 — `InternalsVisibleTo` Elimination and Final Cleanup:**
- [x] Resolve all remaining Clr internal accesses (30 errors → 0). ✅ (2026-03-20)
- [x] Resolve all remaining Compiler internal accesses (44 errors → 0). ✅ (2026-03-20)
- [x] Remove `InternalsVisibleTo("Broiler.JavaScript.Clr")`. ✅ (2026-03-20)
- [x] Remove `InternalsVisibleTo("Broiler.JavaScript.Compiler")`. ✅ (2026-03-20)
- [x] Remove `InternalsVisibleTo("Broiler.JavaScript.Tests")` legacy entry. ✅ (2026-03-20)
- [x] Update downstream consumers (`Broiler.Cli`, `Broiler.App`) to explicit
  satellite assembly references. ✅ (2026-03-20)
- [x] Create `Broiler.JavaScript.All` meta-package. ✅ (2026-03-20)
- [x] Create `.github/workflows/ci.yml` CI workflow. ✅ (2026-03-20)
- [x] Integrate `coverlet` coverage measurement into CI. ✅ (2026-03-20)
- [x] Fix legacy project references, target frameworks, and namespaces
  (Network, ModuleExtensions, NodePollyfill, CLI REPL, JIntPerfTests). ✅ (2026-03-21)
- [x] Fix JSClassGenerator source generator `JSPropertyAttributes` namespace. ✅ (2026-03-21)

**Key Metrics (target):**
- Zero `InternalsVisibleTo` migration bridges remain (test-access-only entries
  are acceptable). ✅ *Achieved — only `Core.Tests`, `Runtime` (dynamic
  assembly), and `WebAtoms.XF` (external) entries remain.*
- `Broiler.JavaScript.Core` no longer contains value type system base types —
  base types and contracts live in Runtime. ✅ *Achieved — `JSValue`,
  `Arguments`, `PropertyKey`, `CoreScript` moved to Runtime. All contract
  interfaces in Runtime. Concrete implementation types (`JSObject`, `JSFunction`,
  `JSContext`) intentionally remain in Core (see Section 26).*
- All downstream consumers build and run against the new assembly structure. ✅
- Each assembly has ≥ 90% line coverage in its dedicated test project. ⏳
  *Coverage collection now enabled via `coverlet.collector` in all 10 test
  projects; CI collects `XPlat Code Coverage` data.*

---

## 6. Implementation Guidance

### 6.1 Cross-Assembly Communication

**Interfaces over concrete types.** Assemblies that consume services from other
assemblies should depend on interfaces, not concrete classes. Key interfaces:

| Interface | Defined In | Implemented In | Status |
|-----------|-----------|----------------|--------|
| `IBuiltInRegistry` | Runtime | BuiltIns, Clr | ✅ Implemented |
| `IClrInterop` | Runtime | Clr | ✅ Implemented |
| `IDebugger` | Runtime | Debugger | ✅ Implemented |
| `IJSCompiler` | Runtime | Compiler | ✅ Implemented (`CoreScript.Compiler`) |
| `IJSModuleResolver` | Runtime | Modules | ✅ Interface defined |

**Registration pattern.** Assemblies register their services at startup:

```csharp
// In Broiler.JavaScript.BuiltIns
public class BuiltInRegistry : IBuiltInRegistry
{
    public void Register(JSContext context)
    {
        // Register Array, String, Number, etc.
    }
}

// In application startup
var context = new JSContext();
new BuiltInRegistry().Register(context);
new DefaultClrInterop().Attach(context);
```

**Event-based hooks.** For optional cross-assembly notifications (e.g.,
debugger script-parsed events), use C# events or delegates defined in Runtime
and subscribed to by downstream assemblies.

### 6.2 Namespace Conventions

Each assembly uses a namespace matching its assembly name:

| Assembly | Root Namespace |
|----------|---------------|
| Broiler.JavaScript.Ast | `Broiler.JavaScript.Ast` |
| Broiler.JavaScript.Parser | `Broiler.JavaScript.Parser` |
| Broiler.JavaScript.Compiler | `Broiler.JavaScript.Compiler` |
| Broiler.JavaScript.Runtime | `Broiler.JavaScript.Runtime` |
| Broiler.JavaScript.Storage | `Broiler.JavaScript.Storage` |
| Broiler.JavaScript.BuiltIns | `Broiler.JavaScript.BuiltIns` |
| Broiler.JavaScript.Clr | `Broiler.JavaScript.Clr` |
| Broiler.JavaScript.Modules | `Broiler.JavaScript.Modules` |
| Broiler.JavaScript.Debugger | `Broiler.JavaScript.Debugger` |

During migration, the old namespaces (e.g., `Broiler.JavaScript.Core.Core`,
`Broiler.JavaScript.Core.FastParser.Ast`) are preserved as `global using`
aliases or `TypeForwardedTo` attributes to avoid breaking downstream code.
These are removed after all consumers have been updated.

### 6.3 `InternalsVisibleTo` Policy

- Use `InternalsVisibleTo` **only** during migration as a temporary bridge.
- Track every `InternalsVisibleTo` usage in a migration spreadsheet / issue.
- Before a milestone is considered complete, all `InternalsVisibleTo` entries
  that were added for migration must be resolved by making the required API
  public or removing the dependency.

**Current `InternalsVisibleTo` Status (Core `AssemblyInfo.cs`):**

| Target Assembly | Purpose | Internal APIs Accessed | Status |
|----------------|---------|----------------------|--------|
| `Broiler.JavaScript.Core.Tests` | Core test project | Test-internal access | ✅ Expected — standard test access |
| `Broiler.JavaScript.Runtime` | Runtime assembly | Dynamic assembly access | ✅ Required for dynamic assembly generation |
| `WebAtoms.XF` | External consumer | Various | ⏳ External dependency — cannot remove unilaterally |

**Fully resolved bridges (no `InternalsVisibleTo` entry remains):**
- ✅ `Broiler.JavaScript.Tests` — legacy entry removed (2026-03-20); project
  does not exist in repository.
- ✅ `Broiler.JavaScript.Debugger` — all internal APIs made public (Phase 4);
  no `InternalsVisibleTo` entry exists.
- ✅ `Broiler.JavaScript.Clr` — **fully resolved** (2026-03-20). All 30
  compilation errors fixed by: `JSString.StringValue` property,
  `JSNumber.NumberValue` property, `JSDate` uses existing public `Value`
  property, `NumberParser.CoerceToNumber` made public, `TypeExtensions` class
  and `GetElementTypeOrGeneric` made public, `JSFunction(delegate,type)`
  constructor made public. Combined with prior work: `JSFunction.Delegate`
  property, `BasePrototypeObject` setter, `SetValue(uint,...)` all made public.
- ✅ `Broiler.JavaScript.Compiler` — **fully resolved** (2026-03-20). All 44
  compilation errors fixed by: `ExpressionHelper` class made public (22
  `ToJSValue` errors), `NewLambdaExpression` class made public (8
  `CallExpression` errors), `ListOfExpressionsExtensions` class and methods
  made public (8 `ConvertTo*` errors), `JSVariable.ValueExpression` made
  public (6 errors). Combined with prior work: `CallStackItemBuilder`,
  `StringSpanBuilder.New()`, `NumberParser.TryCoerceToUInt32`,
  `KeyStringsBuilder`, `JSSpreadValueBuilder`, `ArgumentsBuilder.refType`,
  `JSValueBuilder.StaticEquals`, `JSBigIntBuilder.New()`,
  `JSDecimalBuilder.New()` all made public.

**Previously resolved API changes:**
- ✅ `Broiler.JavaScript.Debugger` — all internal APIs made public (Phase 4);
  no `InternalsVisibleTo` entry exists.
- ✅ `BasePrototypeObject` setter — made public (was used by Clr assembly in 5
  locations; `JSValue.BasePrototypeObject` and `JSObject.BasePrototypeObject`).
- ✅ `KeyStringsBuilder` — made public (was used by Compiler in 2 locations).
- ✅ `JSSpreadValueBuilder` — made public (was used by Compiler in 2 locations).
- ✅ `CallStackItemBuilder` — made public (was used by Compiler in 3 locations).
- ✅ `StringSpanBuilder.New()` methods — made public (was used by Compiler in
  9+ locations).
- ✅ `NumberParser` class and `TryCoerceToUInt32` — made public (was used by
  Compiler in 2 locations).
- ✅ `JSFunction.Delegate` property — added as public accessor for internal `f`
  field (was used by Clr in ClrType.cs constructor, 2 sites).
- ✅ `JSValue.SetValue(uint, ...)` — made public (was `internal protected`);
  cascaded to 14 overrides across `JSObject`, `JSArray`, `JSProxy`, `ClrProxy`,
  and all typed arrays. Matches `GetValue(uint, ...)` which was already public.
- ✅ `ArgumentsBuilder.refType` — made public (was used by Compiler in
  `FastCompiler.cs`).
- ✅ `JSValueBuilder.StaticEquals` — made public (was used by Compiler in
  `FastCompiler.VisitSwitchStatement.cs`).
- ✅ `JSBigIntBuilder.New()` — made public (was used by Compiler in
  `FastCompiler.VisitLiteral.cs` and `FastCompiler.VisitUnaryExpression.cs`).
- ✅ `JSDecimalBuilder.New()` — made public (was used by Compiler in
  `FastCompiler.VisitLiteral.cs`).

### 6.4 Project File Conventions

All new assemblies follow this template:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <LangVersion>latest</LangVersion>
    <AllowUnsafeBlocks>true</AllowUnsafeBlocks>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>

  <ItemGroup>
    <!-- Assembly-specific project references -->
  </ItemGroup>
</Project>
```

- Target `net8.0` to match the existing `Broiler.JavaScript.Core` target.
- Enable nullable reference types for new code.
- Retain `AllowUnsafeBlocks` only for assemblies that require it (Runtime,
  Storage, ExpressionCompiler).

### 6.5 Testing Requirements

Each extracted assembly must have a corresponding test project:

| Assembly | Test Project | Focus | Status |
|----------|-------------|-------|--------|
| Ast | `Broiler.JavaScript.Ast.Tests` | Node construction, token/span types, enum coverage | ✅ 73 tests |
| Parser | `Broiler.JavaScript.Parser.Tests` | Parsing correctness for all JS constructs, tokenization, keyword maps | ✅ 78 tests |
| Compiler | `Broiler.JavaScript.Compiler.Tests` | Expression tree generation, generator rewriting | ✅ 9 tests |
| Runtime | `Broiler.JavaScript.Runtime.Tests` | `IJSModuleResolver` contract, `ExportAttribute`/`DefaultExportAttribute`, `CancellableDisposableAction`, stub implementations, multi-resolver independence | ✅ 20 tests |
| Storage | `Broiler.JavaScript.Storage.Tests` | Property map operations, hash collision handling, `JSPropertyAttributes`, `KeyString`/`KeyStrings` | ✅ 100 tests |
| BuiltIns | `Broiler.JavaScript.BuiltIns.Tests` | WeakRef, FinalizationRegistry, EventTarget, Event, AdditionalRegistrations | ✅ 16 tests |
| Clr | `Broiler.JavaScript.Clr.Tests` | ClrProxy marshalling, ClrType caching, DefaultClrInterop, expression builder registration | ✅ 29 tests |
| Modules | `Broiler.JavaScript.Modules.Tests` | Import/export resolution, circular dependencies | ✅ 9 tests |
| Debugger | `Broiler.JavaScript.Debugger.Tests` | V8 Inspector protocol message handling | ✅ 23 tests |

**Test principles:**
- All tests run on Linux, macOS, and Windows (CI matrix).
- Use xUnit with `Microsoft.NET.Test.Sdk` (matching existing test infrastructure).
- Each test project references only its target assembly (plus test helpers).
- Integration tests that span multiple assemblies live in a dedicated
  `Broiler.JavaScript.Integration.Tests` project.

### 6.6 Downstream Consumer Updates

After the refactor, downstream projects update their references:

| Consumer | Before | After |
|----------|--------|-------|
| `Broiler.App` (WPF) | `Broiler.JavaScript.Core` | `Broiler.JavaScript.Runtime` + `Broiler.JavaScript.BuiltIns` + `Broiler.JavaScript.Compiler` |
| `Broiler.Cli` | `Broiler.JavaScript.Core` | Same as above |
| `Broiler.Avalonia` | `Broiler.JavaScript.Core` | Same as above |
| `Broiler.JavaScript` (CLI) | `Broiler.JavaScript.Core` | All assemblies (full engine) |
| `Broiler.JavaScript.Network` | `Broiler.JavaScript.Core` | `Broiler.JavaScript.Runtime` |

A **meta-package** `Broiler.JavaScript.All` can be introduced as a convenience
reference that transitively includes all engine assemblies, so existing
consumers need only change one `<ProjectReference>`.

---

## 7. Risk Mitigation

| Risk | Mitigation | Status |
|------|------------|--------|
| **Breaking downstream builds** | Use `TypeForwardedTo` attributes and `global using` aliases during migration to preserve API compatibility. | ✅ Active — `TypeForwardedTo` used for `IJSModuleResolver`, AST types, Parser types, Storage types. |
| **Circular dependencies** | Extract shared primitives (`StringSpan`, `KeyString` base) into **Ast** assembly. Use interfaces (`IPropertyValue`, `IPropertyAccessor`, `IClrInterop`) for upward references. | ✅ Complete — `IPropertyContracts` defined in Ast; `IClrInterop` replaces concrete `ClrProxy` refs. Phase 9a resolved Storage↔Runtime cycle: `JSProperty` uses interface-typed fields; `PropertySequence`/`ElementArray` use interface-typed method parameters. |
| **Performance regression** | Assembly boundaries add no runtime cost (same AppDomain, same JIT). Benchmark critical paths (parse → compile → execute) before and after each phase. | ✅ No regressions observed through Phase 8. |
| **Test coverage gaps** | Require each phase to achieve ≥ 90% line coverage on the extracted assembly before merging. | ⏳ All 10 assemblies have dedicated test projects (962 tests total). Coverage measurement tooling (e.g., `coverlet`) to be integrated into CI. |
| **Scope creep** | Each phase is a self-contained PR. No functional changes — only structural moves and interface introductions. | ✅ Active — enforced through Phases 1–8. |
| **Module initializer ordering** | Satellite assemblies (Clr, Compiler, Modules, BuiltIns) register via `[ModuleInitializer]`. Test projects must ensure assemblies are loaded before `JSContext` creation. | ✅ Mitigated — test bootstraps use `RuntimeHelpers.RunModuleConstructor`. Document in contributor guide. |
| **`InternalsVisibleTo` bridge accumulation** | Track all bridges in Section 6.3. Each phase must reduce bridge count — never increase. Target zero migration bridges by Phase 10. | ✅ All migration bridges eliminated. Only `Core.Tests`, `Runtime` (dynamic assembly), and `WebAtoms.XF` (external) entries remain. |
| **External consumer breakage (`WebAtoms.XF`)** | Coordinate with external teams before removing `InternalsVisibleTo` entries. Provide migration guide and deprecation timeline. | ⏳ `WebAtoms.XF` entry retained. No removal planned without external coordination. |

---

## 8. Success Criteria

The refactor is complete when:

1. `Broiler.JavaScript.Core` no longer exists as a single monolithic assembly.
   Its code is distributed across the assemblies defined in Section 2.
2. Each assembly has a dedicated test project with ≥ 90% line coverage.
3. All existing tests in `Broiler.JavaScript.Core.Tests` pass (distributed
   across the new test projects).
4. `Broiler.App`, `Broiler.Cli`, and `Broiler.Avalonia` build and run correctly
   with the new assembly structure.
5. No `InternalsVisibleTo` entries remain as migration bridges.
6. The CI pipeline (`.github/workflows/ci.yml`) builds and tests all new
   assemblies on Linux, macOS, and Windows.
7. No circular assembly dependencies exist (`dotnet list reference` confirms
   unidirectional dependency graph).
8. Downstream build instructions (Section 11) are verified and up to date.

### Current Progress Against Success Criteria

| # | Criterion | Status |
|---|-----------|--------|
| 1 | Core decomposed into separate assemblies | ✅ 8 of 11 target assemblies extracted (Ast, Parser, Storage, Debugger, Clr, BuiltIns, Compiler, Modules). Runtime assembly contains all base types (`JSValue`, `Arguments`, `PropertyKey`, `CoreScript`), all contract interfaces (`IJSContext`, `IJSFunction`, `IDebugger`, `IClrInterop`, `IJSCompiler`, `ICodeCache`, `IBuiltInRegistry`), and interface abstractions (`IJSPrototype`, `IJSSymbol`). Concrete implementation types (`JSObject`, `JSContext`, `JSFunction`) intentionally remain in Core per architectural assessment (Section 26). All storage types fully migrated to Storage assembly. 31 `TypeForwardedTo` attributes maintain backward compatibility. |
| 2 | Each assembly has test project with ≥ 90% coverage | ⏳ 11 extracted/satellite assemblies have dedicated test projects (1024 tests total across 11 projects). Coverage measurement integrated into CI via `coverlet.collector`. Coverage thresholds not yet enforced — see Section 29.1 for action plan. |
| 3 | All existing Core.Tests pass | ✅ 641 Core.Tests pass (verified 2026-03-21). |
| 4 | Downstream consumers build correctly | ✅ Explicit satellite assembly references added to `Broiler.Cli` and `Broiler.App`. `Broiler.JavaScript.All` meta-package available for convenience. |
| 5 | No `InternalsVisibleTo` migration bridges | ✅ All migration bridges eliminated — Debugger (Phase 4), Clr (Phase 10), Compiler (Phase 10). Only `Core.Tests` (test access), `Runtime` (dynamic assembly), and `WebAtoms.XF` (external) entries remain. |
| 6 | CI pipeline covers all assemblies | ✅ `.github/workflows/ci.yml` runs 11 test projects on 3 platforms with `coverlet` code coverage collection. |
| 7 | No circular dependencies | ✅ Verified — all assemblies follow unidirectional dependency graph. Runtime → Storage/Ast; Core → Runtime/Storage/Ast; Satellite assemblies → Core. |
| 8 | Downstream build instructions updated | ✅ Section 11 documents migration steps; `Broiler.Cli` and `Broiler.App` updated with explicit references. |

---

## 9. Implementation Log

### Phase Progress Summary

| Phase | Assembly | Status | Date | Blockers |
|-------|----------|--------|------|----------|
| 1 | Ast | ✅ Complete | 2026-03-19 | — |
| 2 | Parser | ✅ Complete | 2026-03-19 | — |
| 3 | Storage | ✅ Complete | 2026-03-21 | All storage types moved: `JSProperty` ✅ (interface-typed fields); `PropertySequence` ✅ (interface-typed params, `PropertyValueEnumerator` in Core); `ElementArray` ✅ (interface-typed params, `Comparison<IPropertyValue>` sorting); `KeyString`/`KeyStrings`/`KeyType` ✅. |
| 4 | Debugger | ✅ Complete | 2026-03-19 | — |
| 5 | Clr | ✅ Complete | 2026-03-20 | `InternalsVisibleTo` bridge **removed** ✅. |
| 6 | BuiltIns | ✅ Complete (practical limit) | 2026-03-21 | 12 types extracted via factory delegates (Section 31). Remaining candidates (JSArray, JSString, JSNumber, JSError, JSPromise, JSRegExp) assessed as impractical due to deep coupling. |
| 7 | Compiler | ✅ Complete | 2026-03-20 | `InternalsVisibleTo` bridge **removed** ✅. |
| 8 | Modules | ✅ Complete | 2026-03-20 | — |
| 9 | Runtime | ✅ Substantially complete | 2026-03-21 | Phase 9a ✅: all storage types moved to Storage. Phase 9b ✅: core value types (`JSValue`, `Arguments`, `PropertyKey`, `JSFunctionDelegate`, `IElementEnumerator`) moved to Runtime; `IJSPrototype`/`IJSSymbol` interface abstractions created. Phase 9c ✅: all contract interfaces moved to Runtime. Phase 9c+ ✅: `IJSContext` abstraction created; `IBuiltInRegistry` unblocked. Phase 9d ✅: `CoreScript` moved to Runtime via factory delegates; `IJSFunction` interface created. Concrete types (`JSObject`/`JSFunction`/`JSContext`) intentionally remain in Core — architectural decision (see Section 26). |
| 10 | Cleanup | ✅ Complete | 2026-03-20 | All migration bridges removed; meta-package created; downstream consumers updated; CI workflow created; coverlet coverage integrated. |

### Phase 1 — Ast Extraction ✅

**Status:** Complete

**Date:** 2026-03-19

**What was done:**

1. Created `Broiler.JavaScript.Ast` assembly at
   `Broiler.JavaScript/Broiler.JavaScript.Ast/`.
2. Moved **53 AST node files** from `Broiler.JavaScript.Core/FastParser/Ast/`
   into `Broiler.JavaScript.Ast/Ast/`.
3. Moved shared primitive types into the assembly root:
   - `FastNodeType` (enum)
   - `SpanLocation` (struct)
   - `TokenTypes` (enum + extension)
   - `FastKeywords` (enum)
   - `StringSpan` (struct — extracted from the original file, leaving `KeyValue`
     in Core)
   - `StringSpanReader`
   - `FastToken` (refactored — see below)
   - `FastParseException`
   - `ArraySpan<T>` (generic collection span)
   - `UnaryOperator` (enum)
   - `AstCase` (struct, used by `AstSwitchStatement`)
4. All types use the new namespace `Broiler.JavaScript.Ast`.
5. `Broiler.JavaScript.Core` references `Broiler.JavaScript.Ast` via
   `<ProjectReference>`.
6. Added `GlobalUsings.cs` (`global using Broiler.JavaScript.Ast;`) in Core for
   source-level backward compatibility.
7. Added `AstTypeForwarding.cs` with `[assembly: TypeForwardedTo(...)]`
   attributes for binary compatibility.
8. Updated `Broiler.slnx` to include the new project.

**FastToken refactoring (decision point):**

The original `FastToken` constructor contained two runtime dependencies:
- `NumberParser.CoerceToNumber(Span)` for numeric literal parsing.
- `FastKeywordMap.IsKeyword(Span)` for keyword identification and token-type
  reclassification (e.g., `instanceof` → `TokenTypes.InstanceOf`).

These dependencies prevented `FastToken` from being a pure data type in the Ast
assembly. The constructor was refactored to accept pre-computed values
(`double number`, `bool isKeyword`, `FastKeywords keyword`,
`FastKeywords contextualKeyword`). The number parsing and keyword classification
logic was moved to `FastScanner` (the lexer), which is the correct
responsibility boundary — the scanner produces tokens, the tokens store
results.

**StringSpan extraction (decision point):**

The original `StringSpan.cs` in Core contained both the `StringSpan` struct and
an unrelated `KeyValue` struct that depends on `JSValue` (a runtime type).
These were separated: `StringSpan` moved to Ast, `KeyValue` remains in Core.
`StringSpan.GetHashCode()` uses `UnsafeGetHashCode` from
`Broiler.JavaScript.ExpressionCompiler`, so the Ast assembly references
ExpressionCompiler (which has no dependencies on Core, avoiding circular
references).

**Additional types moved (deviation from plan):**

The original plan listed only `AstNode` types, `FastNodeType`, `SpanLocation`,
and `StringSpan` for the Ast assembly. During implementation, the following
types were also moved because AST nodes depend on them directly:

- `FastToken` — stored by every `AstNode` for source location tracking.
- `TokenTypes` — used by `AstLiteral` and `AstBinaryExpression`.
- `FastKeywords` — stored in `FastToken` fields.
- `FastParseException` — thrown during parsing, references AST types.
- `ArraySpan<T>` — generic collection used alongside AST nodes.
- `UnaryOperator` — enum used by `AstUnaryExpression`.
- `AstCase` — struct used by `AstSwitchStatement`.

This is consistent with the document's dependency matrix (Section 2.3), which
states Ast has no dependencies. Moving these types into Ast ensures the
assembly remains self-contained (its only reference is ExpressionCompiler for
`IFastEnumerable<T>`, `Sequence<T>`, and `UnsafeGetHashCode`).

**Verification:**
- `Broiler.JavaScript.Ast` compiles with zero errors.
- `Broiler.JavaScript.Core` compiles with zero errors.
- All **641** tests in `Broiler.JavaScript.Core.Tests` pass.
- All **592** tests in `Broiler.Cli.Tests` pass (7 pre-existing failures in
  `HttpClientMigrationTests` are unrelated — they reference old
  `HtmlRenderer.*.dll` assembly names).
- Downstream consumers (`Broiler.Cli`, `Broiler.App`) build successfully.

---

### Phase 2 — Parser Extraction ✅

**Status:** Complete

**Date:** 2026-03-19

**What was done:**

1. Created `Broiler.JavaScript.Parser` assembly at
   `Broiler.JavaScript/Broiler.JavaScript.Parser/`.
2. Moved **5 infrastructure files** from `Broiler.JavaScript.Core/FastParser/`
   root:
   - `FastScanner.cs` (lexer)
   - `FastKeywordMap.cs` (keyword mapping)
   - `FastTokenStream.cs` (token stream)
   - `CharExtensions.cs` (character utilities)
   - `IParser.cs` (parser interface)
3. Moved **30 parser files** from `Broiler.JavaScript.Core/FastParser/Parser/`:
   - `FastParser.cs` (main parser class)
   - 23 `FastParser.*.cs` partial class files (recursive descent grammar rules)
   - `FastList.cs`, `FastPool.cs`, `FastScope.cs`, `FastScopeItem.cs`,
     `FastStack.cs`, `QueueExtensions.cs`
4. All types use the new namespace `Broiler.JavaScript.Parser`.
5. `Broiler.JavaScript.Core` references `Broiler.JavaScript.Parser` via
   `<ProjectReference>`.
6. Updated `GlobalUsings.cs` (`global using Broiler.JavaScript.Parser;`) in Core
   for source-level backward compatibility.
7. Added `ParserTypeForwarding.cs` with `[assembly: TypeForwardedTo(...)]`
   attributes for binary compatibility.
8. Updated `Broiler.slnx` to include the new project.

**Dependency resolution (decision points):**

The parser files had four dependencies on types defined in
`Broiler.JavaScript.Core`. These were resolved without introducing a reference
from Parser back to Core:

- **`ConcurrentStringMap<FastKeywords>`** (used by `FastKeywordMap`) —
  Replaced with `ConcurrentDictionary<string, FastKeywords>` from
  `System.Collections.Concurrent`. The keyword lookup table is small and
  `ConcurrentDictionary` provides equivalent thread-safe semantics.

- **`StringMap<(StringSpan, FastVariableKind)>`** (used by `FastScopeItem`) —
  Replaced with `Dictionary<string, (StringSpan, FastVariableKind)>`.
  Variable scope tracking uses `name.Value` (string conversion) for dictionary
  keys. This is acceptable because scope tracking during parsing is not a hot
  path and avoids pulling in the custom trie-based map.

- **`NumberParser.CoerceToNumber`** (used by `FastScanner`) —
  Extracted the `CoerceToNumber` method and all its private helper methods
  (`ParseCore`, `ParseHex`, `ParseOctal`, `ParseBinary`,
  `IsWhiteSpaceOrLineTerminator`, `RefineEstimate`, `AddUlps`,
  `ScaleToInteger`) into a new internal `NumberCoercion` class in the Parser
  assembly. The original `NumberParser` remains in Core for runtime use
  (`parseFloat`, `parseInt`).

- **`CancellableDisposableAction`** (used by `FastTokenStream.UndoMark`) and
  **`DisposableList`** (used by `FastPool.Scope`) — These small utility classes
  were recreated as `internal` types in the Parser assembly to avoid a
  dependency on Core.

**Assembly references:**
- `Broiler.JavaScript.Parser` references `Broiler.JavaScript.Ast` (for AST
  node types, `FastToken`, `StringSpan`, etc.) and
  `Broiler.JavaScript.ExpressionCompiler` (for `LinkedStack<T>`,
  `LinkedStackItem<T>`, `IFastEnumerable<T>`, `Sequence<T>`).
- The `FastParser/Compiler/` directory remains in Core (it is the compiler,
  not the parser). Compiler files use `global using Broiler.JavaScript.Parser;`
  to access parser types.

**Verification:**
- `Broiler.JavaScript.Parser` compiles with zero errors.
- `Broiler.JavaScript.Core` compiles with zero errors.
- All **641** tests in `Broiler.JavaScript.Core.Tests` pass.

---

### Phase 3 — Storage Extraction ✅ (Partial)

**Status:** Partial — pure storage types extracted

**Date:** 2026-03-19

**What was done:**

1. Created `Broiler.JavaScript.Storage` assembly at
   `Broiler.JavaScript/Broiler.JavaScript.Storage/`.
2. Moved **7 pure storage files** from `Broiler.JavaScript.Core/Core/Storage/`:
   - `VirtualMemory<T>` and `VirtualArray` (dynamic memory management)
   - `SAUint32Map<T>` (sparse array-based uint-to-T trie)
   - `StringMap<T>` (hash-based trie for StringSpan/HashedString keys)
   - `HashedString` (struct caching StringSpan hash)
   - `ConcurrentStringMap<T>`, `ConcurrentNameMap`, `ConcurrentUInt32Map<T>`
     (thread-safe string/uint maps)
   - `ConcurrentTypeCache` (static type-to-ID cache)
   - `ConcurrentTypeTrie<T>` (type-to-T cache)
3. All types use the new namespace `Broiler.JavaScript.Storage`.
4. `Broiler.JavaScript.Core` references `Broiler.JavaScript.Storage` via
   `<ProjectReference>`.
5. Updated `GlobalUsings.cs` (`global using Broiler.JavaScript.Storage;`) in Core
   for source-level backward compatibility.
6. Added `StorageTypeForwarding.cs` with `[assembly: TypeForwardedTo(...)]`
   attributes for binary compatibility.
7. Updated `Broiler.slnx` to include the new project.

**Accessibility changes:**

- `ConcurrentTypeCache` changed from `internal` to `public` (used by
  `MarshalExtensions` in Core).
- `ConcurrentTypeTrie<T>` changed from `internal` to `public` (same reason).
- `SAUint32Map<T>.Resize` changed from `internal` to `public` (used by
  `ElementArray` in Core).
- `ConcurrentUInt32Map<T>.GetOrCreate` overloads changed from `internal` to
  `public` (used by `ModuleCache` and `ClrType` in Core).

**Types that remain in Core:**

Three storage types depend on runtime types (`JSValue`, `JSFunction`,
`JSObject`, `JSContext`, `KeyString`) and were **not** extracted:

- `JSProperty` (struct) — references `JSFunction`, `JSValue`, `KeyString`.
- `PropertySequence` (struct) — references `JSObject`, `JSContext`, `KeyString`.
- `ElementArray` (struct) — references `JSProperty`, `JSValue`, `JSFunction`.

These will be moved when the Runtime assembly is extracted, resolving the
circular dependency described in Section 2.3.

**Verification:**
- `Broiler.JavaScript.Storage` compiles with zero errors (depends only on Ast).
- `Broiler.JavaScript.Core` compiles with zero errors.
- All **641** tests in `Broiler.JavaScript.Core.Tests` pass.

**Test project (2026-03-20):**

8. Created `Broiler.JavaScript.Storage.Tests` test project at
   `Broiler.JavaScript/Broiler.JavaScript.Storage.Tests/`.
9. Added **56 assembly-specific tests** covering all extracted storage types:
   - `VirtualMemoryTests` — allocation, indexing, capacity management.
   - `SAUint32MapTests` — save, get, remove, put-ref, resize, all-values enumeration.
   - `StringMapTests` — save, get, remove, put-ref, HashedString overloads.
   - `HashedStringTests` — construction, equality, comparison, implicit conversions.
   - `ConcurrentMapTests` — `ConcurrentStringMap<T>`, `ConcurrentNameMap`,
     `ConcurrentUInt32Map<T>` thread-safe get/set/create operations.
   - `ConcurrentTypeCacheTests` — `ConcurrentTypeCache` ID consistency,
     `ConcurrentTypeTrie<T>` factory caching.
10. Test project references only `Broiler.JavaScript.Storage` (no Core dependency).
11. All 56 tests pass.

---

### Phase 4 — Debugger Extraction ✅ (Partial)

**Status:** Partial — V8 Inspector Protocol extracted

**Date:** 2026-03-19

**What was done:**

1. Created `Broiler.JavaScript.Debugger` assembly at
   `Broiler.JavaScript/Broiler.JavaScript.Debugger/`.
2. Moved **26 V8 Inspector Protocol files** from
   `Broiler.JavaScript.Core/Debugger/`:
   - `V8InspectorProtocol` (abstract protocol handler, extends `JSDebugger`)
   - `V8InspectorProtocolProxy` (WebSocket transport)
   - `V8Debugger` + 4 partial files (debugger domain handler)
   - `V8Runtime` + 10 partial files (runtime domain handler)
   - `V8RemoteObject`, `V8ReturnValue`, `V8ExceptionDetails`,
     `V8PropertyDescriptor`, `V8StackTrace` (protocol data types)
   - `V8ProtocolEvent`, `V8ProtocolObject` (protocol infrastructure)
   - `AsyncQueue<T>`, `HashExtensions` (utilities)
3. All types use the new namespace `Broiler.JavaScript.Debugger`.
4. The Debugger assembly references Core via `<ProjectReference>`.
5. Updated `Broiler.slnx` to include the new project.

**Types that remain in Core:**

- `IDebugger` (interface) — consumed by `JSContext.Debugger` property.
- `JSDebugger` (abstract class) — provides `RaiseBreak()` static method used by
  `JSDebuggerBuilder` and `FastCompiler` for `debugger;` statement support.
- `JSConsole` (in `Core/Debug/`) — created directly by `JSContext` constructor.

**Dependency direction:**

The Debugger assembly references Core (upward dependency). Core does **not**
reference the Debugger assembly. This means:

- No `TypeForwardedTo` attributes are used (they require a downward reference).
- Consumers who need V8 Inspector Protocol must reference the Debugger assembly
  directly.
- `InternalsVisibleTo("Broiler.JavaScript.Debugger")` was added to Core's
  `AssemblyInfo.cs` as a temporary migration bridge (per Section 4.1 #3). The
  internal APIs accessed are: `JSContext.Top`, `KeyStrings.GetNameString`,
  `JSPrototype.propertySet`, `CoreScript.Compile`, `JSValue.StringValue`,
  `JSValue.IsNullOrUndefined`, `JSValue.GetValue`.

**Verification:**
- `Broiler.JavaScript.Debugger` compiles with zero errors.
- `Broiler.JavaScript.Core` compiles with zero errors.
- All **641** tests in `Broiler.JavaScript.Core.Tests` pass.

**Test project (2026-03-20):**

7. Created `Broiler.JavaScript.Debugger.Tests` test project at
   `Broiler.JavaScript/Broiler.JavaScript.Debugger.Tests/`.
8. Added **23 assembly-specific tests** covering V8 Inspector Protocol types:
   - `HashExtensionsTests` — SHA256 hash computation, consistency, empty input.
   - `AsyncQueueTests` — enqueue, dispose, async process yielding.
   - `V8CallFrameTests` — property construction and defaults.
   - `V8ReturnValueTests` — default construction, exception wrapping, implicit
     conversion.
   - `V8ExceptionDetailsTests` — exception text extraction, JSException handling.
   - `V8RemoteObjectTests` — JSValue type mapping (undefined, null, string,
     number, boolean).
9. Test project references `Broiler.JavaScript.Debugger` and
   `Broiler.JavaScript.Core` (Debugger types require Core runtime types).
10. All 23 tests pass.

---

### Phase 5–8 — Dependency Analysis

**Status:** In progress — prerequisite infrastructure implemented

**Date:** 2026-03-19 (initial analysis), 2026-03-20 (IBuiltInRegistry + IClrInterop
implemented, direct ClrProxy/ClrType calls refactored, JSClassGenerator updated,
TryUnwrapClrObject added, structural type checks converted)

**Prerequisite infrastructure (completed):**

Two key interfaces were implemented to unblock Phases 5–6:

1. **`IBuiltInRegistry`** (in `Core/IBuiltInRegistry.cs`) — Defines a pluggable
   contract for registering built-in JavaScript objects (Array, String, Date,
   Promise, etc.) into a `JSContext`. This replaces hard-coded type
   initialization in the constructor.
   - `DefaultBuiltInRegistry` implements the interface, calling
     `RegisterGeneratedClasses()` (source-generated) and setting up the
     Iterator.prototype chain (ES2025 helpers).
   - `JSContext.BuiltInRegistry` is a static property defaulting to
     `DefaultBuiltInRegistry.Instance`. Custom implementations can be swapped in.
   - The `JSContext` constructor calls `BuiltInRegistry.Register(this)` instead
     of directly initializing built-in types.

2. **`IClrInterop`** (in `Core/Clr/IClrInterop.cs`) — Defines a pluggable
   contract for marshalling between .NET objects and JavaScript values:
   - `Marshal(object value)` — converts .NET objects to `JSValue`.
   - `GetClrType(Type type)` — returns JS class wrapper for a .NET `Type`.
   - `TryUnwrapClrObject(JSValue value, out object clrObject)` — checks if a
     `JSValue` is a CLR proxy and extracts the underlying .NET object. Replaces
     direct `is ClrProxy` type checks so non-Clr assemblies can inspect proxy
     values without referencing the concrete type.
   - `DefaultClrInterop` implements the interface, delegating to the existing
     `ClrProxy.Marshal()` and `ClrType.From()` static methods.
   - `JSContext.ClrInterop` is a static property defaulting to
     `DefaultClrInterop.Instance`.

**IClrInterop refactoring (completed):**

Direct `ClrProxy.Marshal()` and `ClrType.From()` calls in Core files outside
`Core/Clr/` were refactored to use `JSContext.ClrInterop`:

| File | Before | After |
|------|--------|-------|
| `Extensions/MarshalExtensions.cs` | `ClrProxy.Marshal(value)` | `JSContext.ClrInterop.Marshal(value)` |
| `Extensions/MarshalExtensions.cs` | `ClrType.From(type)` | `JSContext.ClrInterop.GetClrType(type)` |
| `Core/Function/JSMethodGroup.cs` (×2) | `ClrProxy.Marshal(method.Invoke(...))` | `JSContext.ClrInterop.Marshal(method.Invoke(...))` |
| `Core/Module/JSModuleContext.cs` (×4) | `ClrProxy.Marshal(result/task)` | `ClrInterop.Marshal(result/task)` |
| `Core/Promise/JSPromiseExtensions.cs` | `ClrProxy.Marshal(result)` | `JSContext.ClrInterop.Marshal(result)` |
| `Enumerators/OwnEntriesEnumerator.cs` (×4) | `ClrProxy.Marshal(en.Current)` | `JSContext.ClrInterop.Marshal(en.Current)` |
| `Core/JSContext.cs` | `ClrProxy.From(new JSConsole(this))` | `ClrInterop.Marshal(new JSConsole(this))` — `ClrProxy.From()` wraps an object in a proxy; `IClrInterop.Marshal()` handles the same case (complex objects are wrapped in a proxy by the default implementation) |
| `Core/Global/JSGlobal.cs` | `ClrType.From(typeof(JSIntl))` | `JSContext.ClrInterop.GetClrType(typeof(JSIntl))` |
| `Core/Intl/JSIntl.cs` (×2) | `ClrType.From(typeof(...))` | `JSContext.ClrInterop.GetClrType(typeof(...))` |

This eliminated **15** direct `ClrProxy.Marshal()`/`ClrType.From()` calls from
non-Clr source files, reducing the remaining direct references from 30 to 15.

**Structural references refactored (completed 2026-03-20):**

| File | Before | After |
|------|--------|-------|
| `Core/JSValue.cs` | `is ClrProxy proxy` (type check) | `JSContext.ClrInterop.TryUnwrapClrObject()` |
| `Extensions/JSValueExtensions.cs` | `is ClrProxy proxy` (type check) | `JSContext.ClrInterop.TryUnwrapClrObject()` |
| `IJavaScriptObject.cs` | `ClrProxy.From(@object)` | `JSContext.ClrInterop.Marshal(@object)` |

**Generated code refactoring (completed 2026-03-20):**

The JSClassGenerator was updated in two files:
- `ListExtensions.cs`: `ClrProxy.Marshal({target})` → `JSContext.ClrInterop.Marshal({target})`
- `ClassGenerator.cs`: `ClrProxy.Marshal(@return)` → `JSContext.ClrInterop.Marshal(@return)`

This converted **68** generated `ClrProxy.Marshal()` calls across **23** `.g.cs`
files to use `JSContext.ClrInterop.Marshal()`, eliminating the last bulk
dependency on the concrete `ClrProxy` type from generated code.

**Remaining ClrProxy/ClrType references (source code, outside Core/Clr/):**

~~The following references cannot be converted to `IClrInterop` calls because they
require the concrete type (for type checks, expression tree reflection, or type
parameters):~~

Most former structural references have been refactored.  The remaining ones are
limited to the expression tree builder and a constructor parameter:

| File | Reference | Status |
|------|-----------|--------|
| ~~`Core/JSValue.cs`~~ | ~~`is ClrProxy proxy`~~ | ✅ Replaced with `IClrInterop.TryUnwrapClrObject()` |
| ~~`Extensions/JSValueExtensions.cs`~~ | ~~`is ClrProxy proxy`~~ | ✅ Replaced with `IClrInterop.TryUnwrapClrObject()` |
| ~~`Core/Function/JSFunction.cs`~~ | ~~`ClrType type` (constructor parameter)~~ | ✅ Changed to `JSFunction type` — decoupled from concrete `ClrType` |
| `Core/Function/JSFunction.cs` | `ClrProxyBuilder.Marshal(inP)` | ⏳ Expression tree generation — inherent to compiler |
| `LinqExpressions/ClrProxyBuilder.cs` | `typeof(ClrProxy)`, `nameof(ClrProxy.Marshal)` | ⏳ Reflection-based expression tree builder — needs concrete type to generate IL |
| ~~`IJavaScriptObject.cs`~~ | ~~`ClrProxy.From(@object)`~~ | ✅ Replaced with `JSContext.ClrInterop.Marshal(@object)` |
| `Extensions/ClrProxyExtensions.cs` | Utility class for CLR enumerators | ⏳ References Clr types structurally (no direct ClrProxy/ClrType dependency) |

**Remaining ClrProxy references (generated code):**

~~The `JSClassGenerator` Roslyn source generator produces `.g.cs` files that call
`ClrProxy.Marshal()` for property getters and method return values.~~

✅ **Resolved** — The JSClassGenerator source generator (`ListExtensions.cs` and
`ClassGenerator.cs`) has been updated to emit `JSContext.ClrInterop.Marshal()`
instead of `ClrProxy.Marshal()`.  All **68** generated code references across
**23** `.g.cs` files now use the `IClrInterop` interface.  The generated code's
`using Broiler.JavaScript.Core.Core.Clr;` import is retained for forward
compatibility but is no longer required for the marshal calls.

**Updated findings for Phases 5–8:**

**Phase 5 (Clr) — mostly unblocked:**
- `IClrInterop` interface is feature-complete: `Marshal()`, `GetClrType()`,
  `TryUnwrapClrObject()`.
- All direct `ClrProxy.Marshal()`/`ClrType.From()` method calls (source + generated)
  have been refactored to use `IClrInterop`.
- All `is ClrProxy` type checks outside `Core/Clr/` have been refactored to use
  `IClrInterop.TryUnwrapClrObject()`.
- `IJavaScriptObject.handle` creation now uses `JSContext.ClrInterop.Marshal()`.
- `JSFunction` constructor decoupled from `ClrType` — now accepts `JSFunction type`
  instead of `ClrType type`.
- **2 structural references remain** in source code:
  - `ClrProxyBuilder` (expression tree builder) — uses `typeof(ClrProxy)` and
    `nameof(ClrProxy.Marshal)` for reflection-based IL generation. This is
    inherent to the compilation model and will be addressed during Phase 7
    (Compiler extraction).
  - `ClrProxyBuilder.Marshal(inP)` in `JSFunction.cs` — expression tree
    generation, also inherent to the compiler.

**Phase 6 (BuiltIns) — unblocked:**
- `IBuiltInRegistry` is fully implemented with `DefaultBuiltInRegistry`.
- `JSContext` constructor uses the pluggable registry pattern.
- JSClassGenerator now emits `JSContext.ClrInterop.Marshal()` — ✅ done.
- ~~**Remaining blocker:** JSClassGenerator must support configurable namespace
  roots / multi-assembly code generation.~~ — ✅ Resolved. The stale
  `using Broiler.JavaScript.Core.Core.Clr;` import was removed from generated
  code (no longer needed since generated code uses `JSContext.ClrInterop.Marshal()`
  which resolves via the property's declared return type). The generator already
  supports multi-assembly generation — the `Broiler.JavaScript.Network` assembly
  demonstrates this pattern. Each assembly needs its own `Names` class with
  `[JSRegistrationGenerator]` and references the generator as an Analyzer.
- **Ready for extraction:** Create the `Broiler.JavaScript.BuiltIns` assembly,
  add a `Names` class with `[JSRegistrationGenerator]`, move built-in type files,
  and update `DefaultBuiltInRegistry` to call the BuiltIns `RegisterAll`.

**Phase 7 (Compiler) — unblocked:**
- `FastCompiler` references runtime types extensively in expression tree
  generation. These references are inherent to the compilation model.
- ~~Extraction requires stable Runtime interfaces.~~ — ✅ `IJSCompiler` interface
  already exists in `FastParser/Compiler/IJSCompiler.cs` and is wired into
  `CoreScript.Compiler`. The `DefaultJSCompiler` class (which creates
  `FastCompiler` instances) is the implementation to extract. After extraction,
  `IJSCompiler` stays in Runtime, `DefaultJSCompiler` and `FastCompiler` move
  to the Compiler assembly, and the Compiler assembly registers itself via
  `CoreScript.Compiler = new DefaultJSCompiler()`.
- The compiler references concrete Runtime types (`JSValue`, `JSContext`,
  `Arguments`, `JSFunction`) because the expression trees it builds contain
  calls to these types' methods. This is acceptable — the Compiler assembly
  depends on Runtime (not the reverse), consistent with the dependency graph.

**Phase 8 (Modules) — partially unblocked:**
- `JSModule` extends `JSObject` and uses `[JSFunctionGenerator]`.
- `JSModuleContext` extends `JSContext`.
- Module files now use `ClrInterop.Marshal()` instead of `ClrProxy.Marshal()`,
  and the `ClrModule.Default` reference has been replaced with the
  `ClrModuleProvider` delegate pattern.
- ~~Namespace-linked generated partial class issues remain.~~ — ✅ Resolved.
  Generator produces partial classes in the type's namespace regardless of
  assembly. When `JSModule` moves to Modules assembly, its generated partial
  class uses the new namespace.
- `IJSModuleResolver` interface defined in Core for pluggable module resolution.
- **No circular dependency:** `JSModuleContext → JSContext` is a clean
  upward dependency. `JSContext` does not reference `JSModuleContext` (only in
  a comment). The Modules assembly follows the same upward-dependency pattern
  as Debugger and Clr.
- **Ready for extraction:** Create `Broiler.JavaScript.Modules` assembly, move
  `JSModuleContext`, `JSModule`, and `ModuleCache`, add a `Names` class with
  `[JSRegistrationGenerator]`.

**Updated recommended next steps (priority order):**
1. ~~Implement `IBuiltInRegistry` pluggable bootstrap~~ — ✅ Done.
2. ~~Refactor Core to use `IClrInterop` for method calls~~ — ✅ Done (15 source
   calls + 68 generated code calls converted).
3. ~~Resolve type check references (`is ClrProxy`)~~ — ✅ Done via
   `IClrInterop.TryUnwrapClrObject()`.
4. ~~Configure `JSClassGenerator` to emit `JSContext.ClrInterop.Marshal()` instead
   of `ClrProxy.Marshal()`~~ — ✅ Done.
5. ~~Resolve `JSFunction` constructor `ClrType` parameter~~ — ✅ Done (changed to
   `JSFunction type`).
6. Resolve remaining structural `ClrProxyBuilder` references (2 call sites in
   expression tree builder — inherent to compiler, Phase 7).
7. ~~Configure `JSClassGenerator` multi-assembly namespace support~~ — ✅ Done.
   Stale Clr import removed; multi-assembly generation verified.
8. ~~Define stable Runtime interfaces for compiler consumption~~ — ✅ Done.
   `IJSCompiler` already exists. `IJSModuleResolver` defined.
9. **Next:** Extract `Broiler.JavaScript.BuiltIns` assembly (Phase 6 file moves).
10. **Next:** Extract `Broiler.JavaScript.Compiler` assembly (Phase 7 file moves).
11. **Next:** Extract `Broiler.JavaScript.Modules` assembly (Phase 8 file moves).

**Estimated effort for remaining work:**
- Remaining structural `ClrProxyBuilder` references: Low — 2 call sites in
  expression tree builder; tightly coupled to compilation model. Will be
  resolved naturally during Phase 7 (Compiler extraction).
- BuiltIns extraction: Medium — move 100+ built-in type files to new assembly,
  create `Names` class, wire up `DefaultBuiltInRegistry`.
- Compiler extraction: Medium — move `FastCompiler`, `DefaultJSCompiler`, and
  expression builder files; `IJSCompiler` stays in Core; register via
  module initializer.
- Modules extraction: Low — move 4 module files; `JSModuleContext` continues
  to extend `JSContext`; register via module initializer.

### Continued Implementation Progress (2026-03-20)

**What was done:**

1. **JSFunction constructor decoupled from `ClrType`** — The `JSFunction`
   constructor parameter `ClrType type` was changed to `JSFunction type`,
   removing a structural dependency between the Function subsystem and the
   concrete Clr type. Since `ClrType : JSFunction`, the change is fully
   backward-compatible. This reduces the remaining structural `ClrProxy`/`ClrType`
   references from 3 to 2 (both in the expression tree builder, Phase 7).

2. **Debugger `InternalsVisibleTo` bridge removed** — Made 8 internal Core APIs
   public so the Debugger assembly no longer requires `InternalsVisibleTo`:
   - `JSContext.Top` (field)
   - `KeyStrings.GetNameString()` (method)
   - `JSValue.IsNullOrUndefined` (property)
   - `JSValue.StringValue` (virtual property — cascaded to `JSSymbol` override)
   - `JSValue.GetValue(uint, JSValue, bool)` (virtual method — cascaded to
     10 typed array overrides + `JSObject`, `JSProxy`, `JSString`, `ClrProxy`)
   - `JSPrototype.JSPropertySet` (nested class + 5 fields)
   - `CoreScript.Compile()` (static method)
   - `StringExtensions` (extension class)
   - `JSPropertyExtensions` (extension class — `GetValue(JSValue, JSProperty)`)

3. **Test projects created:**
   - `Broiler.JavaScript.Ast.Tests` — 73 assembly-specific tests covering
     `FastToken`, `StringSpan`, `SpanLocation`, `FastNodeType`, `TokenTypes`,
     `FastKeywords`, and AST node construction (`AstLiteral`, `AstIdentifier`,
     `AstExpressionStatement`, `AstReturnStatement`).
   - `Broiler.JavaScript.Parser.Tests` — 78 assembly-specific tests covering
     `FastParser` (38 tests: statements, expressions, control flow, functions,
     classes, try/catch, ES2015+ features, error handling), `FastScanner`
     (20 tests: tokenization, keywords, operators, comments, location tracking),
     `FastTokenStream` (7 tests: construction, buffering, EOF),
     `FastKeywordMap` (3 tests: keyword recognition).

4. **Solution files updated:**
   - `Broiler.slnx` — Added `Broiler.JavaScript.Ast.Tests` and
     `Broiler.JavaScript.Parser.Tests`.
   - `YantraJS.sln` — Removed all broken old-name project references
     (`YantraJS.Core`, `YantraJS.ExpressionCompiler`, etc.) and added all
     current Broiler.JavaScript projects (17 projects total including
     5 test projects).

5. **Roadmap document updated** — This section documents Phase 5–6 progress,
   test project status, and updated next steps.

**Verification:**
- `Broiler.JavaScript.Core` compiles with zero errors.
- `Broiler.JavaScript.Debugger` compiles with zero errors (no `InternalsVisibleTo`).
- All **641** tests in `Broiler.JavaScript.Core.Tests` pass.
- All **73** tests in `Broiler.JavaScript.Ast.Tests` pass.
- All **78** tests in `Broiler.JavaScript.Parser.Tests` pass.
- All **56** tests in `Broiler.JavaScript.Storage.Tests` pass.
- All **23** tests in `Broiler.JavaScript.Debugger.Tests` pass.
- **Total: 871 tests across 5 test projects, all passing.**

---

### Phase 5 — Clr Extraction ✅

**Status:** Complete

**Date:** 2026-03-20

**What was done:**

1. Created `Broiler.JavaScript.Clr` assembly at
   `Broiler.JavaScript/Broiler.JavaScript.Clr/`.
2. Moved **11 source files** from `Broiler.JavaScript.Core`:
   - From `Core/Clr/`: `ClrProxy.cs`, `ClrType.cs`, `ClrTypeBuilder.cs`,
     `ClrTypeExtensions.cs`, `ClrModule.cs`, `DefaultClrInterop.cs`,
     `JSFieldInfo.cs`, `JSPropertyInfo.cs`, `JSMethodInfo.cs`,
     `MethodNamesExtensions.cs`.
   - From `Core/Function/`: `JSMethodGroup.cs` (CLR method group wrapper —
     logically belongs with CLR interop types).
3. All moved types use the new namespace `Broiler.JavaScript.Clr`.
4. The Clr assembly references Core via `<ProjectReference>` (upward
   dependency, same pattern as Debugger).
5. Core does **not** reference the Clr assembly — no `TypeForwardedTo`
   attributes, no `global using` aliases.
6. Updated `Broiler.slnx` to include both `Broiler.JavaScript.Clr` and
   `Broiler.JavaScript.Clr.Tests`.

**Types that remain in Core (by design):**

- `IClrInterop` (interface contract — consumed by Core, implemented by Clr).
- `ClrMemberNamingConvention` (used by `JSContext.ClrMemberNamingConvention`).
- `JSExportAttribute`, `JSExportSameNameAttribute` (metadata attributes used
  by the source generator across assemblies).
- `FallbackClrInterop` (fallback `IClrInterop` for when Clr assembly is not
  loaded — handles primitives and JSValue pass-through).

**Decoupling changes (decision points):**

1. **ClrProxyBuilder refactored to delegate pattern** — The expression tree
   builder (`LinqExpressions/ClrProxyBuilder.cs`) was the last structural
   dependency between Core and the concrete `ClrProxy` type.  It used
   `typeof(ClrProxy)` reflection to build method lookup tables.

   Resolution: `ClrProxyBuilder` was converted to a thin dispatcher with
   `Register(Func<Expression, Expression> marshal, Func<Expression, Expression>
   from)`.  The actual implementation (`ClrExpressionBuilder`) lives in the Clr
   assembly and is registered via the assembly's module initializer.  The API
   surface (`.Marshal()` and `.From()` methods) remains identical — all callers
   (including `JSFunction.CreateClrDelegate()`) are unchanged.

2. **FallbackClrInterop as default** — `JSContext.ClrInterop` now defaults to
   `FallbackClrInterop.Instance` (instead of `DefaultClrInterop.Instance`).
   `FallbackClrInterop` handles primitives (int, string, bool, etc.) and
   JSValue pass-through.  For complex CLR objects it returns
   `JSUndefined.Value`.  The full `DefaultClrInterop` is set by the Clr
   assembly's module initializer, which runs when the assembly is loaded.

3. **Console registration moved to DefaultBuiltInRegistry** — The
   `JSContext` constructor previously called
   `ClrInterop.Marshal(new JSConsole(this))` directly.  This was moved into
   `DefaultBuiltInRegistry.Register()` so that console setup happens through
   the pluggable registry, not hardcoded in the constructor.

4. **JSModuleContext ClrModule decoupled** — `JSModuleContext` previously
   referenced `ClrModule.Default` directly.  This was replaced with a static
   `ClrModuleProvider` delegate (`Func<JSObject>`) that the Clr assembly sets
   during initialization.  If the Clr assembly is not loaded, no CLR module is
   registered (the `enableClrIntegration` flag still controls this).

5. **Clr assembly module initializer** — A `[ModuleInitializer]` in the Clr
   assembly (`ClrAssemblyInitializer.cs`) registers:
   - `JSContext.ClrInterop = DefaultClrInterop.Instance`
   - `ClrProxyBuilder.Register(ClrExpressionBuilder.Marshal,
     ClrExpressionBuilder.From)`
   - `JSModuleContext.ClrModuleProvider = () => ClrModule.Default`

   This runs automatically when the assembly is loaded, before any Clr type
   is accessed.

6. **Test project bootstrapping** — Both `Broiler.JavaScript.Core.Tests` and
   `Broiler.JavaScript.Clr.Tests` include a `[ModuleInitializer]` that
   forces the Clr assembly to load (by accessing `typeof(DefaultClrInterop)`),
   ensuring the full CLR interop is available before any test creates a
   `JSContext`.

**Debugger fix:**

- `V8Runtime.CallArgument.ToJSValue()` previously called
  `ClrProxy.Marshal(Value)` directly.  Changed to
  `JSContext.ClrInterop.Marshal(Value)` to use the interface pattern
  (consistent with all other non-Clr code).

**Accessibility changes:**

- `InternalsVisibleTo("Broiler.JavaScript.Clr")` was added to Core's
  `AssemblyInfo.cs` as a temporary migration bridge (per Section 4.1 #3).

**New files in Clr assembly:**

- `ClrExpressionBuilder.cs` — The actual expression tree building logic
  (formerly in `ClrProxyBuilder`), using `typeof(ClrProxy)` reflection.
- `ClrAssemblyInitializer.cs` — Module initializer for registration.

**Test project (2026-03-20):**

7. Created `Broiler.JavaScript.Clr.Tests` test project at
   `Broiler.JavaScript/Broiler.JavaScript.Clr.Tests/`.
8. Added **29 assembly-specific tests** covering:
   - `ClrProxyTests` — `Marshal()` for null, int, string, bool, complex
     objects, JSValue pass-through, `Type` → `ClrType`; `From()` factory
     methods.
   - `ClrTypeTests` — `From()` returns `ClrType`, caching consistency,
     different types produce different instances, `ClrType` is `JSFunction`.
   - `DefaultClrInteropTests` — singleton pattern, `IClrInterop` contract,
     `Marshal()` primitives and complex objects, `GetClrType()`,
     `TryUnwrapClrObject()` for both proxy and non-proxy values.
   - `ClrExpressionBuilderTests` — verifies `ClrProxyBuilder.Marshal()` and
     `.From()` are registered and produce valid expression nodes; JSValue
     pass-through optimization.
   - `ClrAssemblyInitializationTests` — verifies `JSContext.ClrInterop` is
     `DefaultClrInterop` when Clr assembly is loaded; end-to-end
     `JSContext.Eval()` works correctly.
   - `ClrModuleTests` — `ClrModule.Default` is not null and is a `JSValue`.
9. Test project references `Broiler.JavaScript.Clr` and
   `Broiler.JavaScript.Core`.
10. All 29 tests pass.

**Verification:**
- `Broiler.JavaScript.Clr` compiles with zero errors.
- `Broiler.JavaScript.Core` compiles with zero errors.
- `Broiler.JavaScript.Debugger` compiles with zero errors.
- All **641** tests in `Broiler.JavaScript.Core.Tests` pass.
- All **73** tests in `Broiler.JavaScript.Ast.Tests` pass.
- All **78** tests in `Broiler.JavaScript.Parser.Tests` pass.
- All **56** tests in `Broiler.JavaScript.Storage.Tests` pass.
- All **23** tests in `Broiler.JavaScript.Debugger.Tests` pass.
- All **29** tests in `Broiler.JavaScript.Clr.Tests` pass.
- **Total: 900 tests across 6 test projects, all passing.**

---

### Phase 6 — BuiltIns Unblocking (Continued)

**Status:** Unblocked — ready for file extraction

**Date:** 2026-03-20

**What was done:**

1. **JSClassGenerator multi-assembly support verified and cleaned up:**
   - Removed stale `using Broiler.JavaScript.Core.Core.Clr;` import from
     generated code in both `ClassGenerator.cs` and `RegistrationGenerator.cs`.
     This import was a leftover from the pre-Phase 5 era when generated code
     called `ClrProxy.Marshal()` directly. Since generated code now uses
     `JSContext.ClrInterop.Marshal()`, the `IClrInterop` type is resolved
     through the `JSContext.ClrInterop` property's declared return type — the
     caller does not need to import the `Core.Core.Clr` namespace.
   - Verified that the generator already supports multi-assembly code generation.
     The `Broiler.JavaScript.Network` assembly demonstrates the pattern: each
     assembly has its own `Names` class with `[JSRegistrationGenerator]`, its
     own `RegisterAll` method, and references `JSClassGenerator` as an Analyzer.
     The generator uses `type.ContainingNamespace` for namespace resolution, so
     generated code is correctly placed regardless of which assembly contains
     the types.
   - Build warnings reduced from 66 to 41 (removing the unused import
     eliminated 25 "unnecessary using directive" warnings across 25 generated
     files).

2. **`IJSModuleResolver` interface defined:**
   - Created `IJSModuleResolver` in `Core/Module/IJSModuleResolver.cs` as the
     stable interface contract for module path resolution and source loading.
   - Methods: `Resolve(string currentPath, string moduleName)` → `string?`,
     `LoadSourceAsync(string resolvedPath)` → `Task<string>`.
   - This interface follows the same pattern as `IClrInterop`, `IBuiltInRegistry`,
     and `IDebugger` — defined in Runtime, implemented by the Modules assembly.

3. **`IJSCompiler` interface documented as existing:**
   - Discovered that `IJSCompiler` already exists in
     `FastParser/Compiler/IJSCompiler.cs` with a single method:
     `Compile(in StringSpan code, string location, IList<string> argsList,
     ICodeCache codeCache) → YExpression<JSFunctionDelegate>`.
   - `DefaultJSCompiler` implements it by creating `FastCompiler` instances.
   - `CoreScript.Compiler` is a pluggable static property defaulting to
     `DefaultJSCompiler`.
   - Phase 7 (Compiler extraction) is less blocked than previously documented —
     the interface and pluggable pattern are already in place.

4. **Module circular dependency resolved (documentation):**
   - Confirmed that `JSModuleContext → JSContext` is a clean upward dependency.
     `JSContext` does not reference `JSModuleContext` (only in a comment).
   - The `ClrModuleProvider` delegate pattern (from Phase 5) already decouples
     the module system from the Clr assembly.
   - After extraction, the Modules assembly follows the same pattern as Debugger
     and Clr: it references Core (upward dependency), Core does not reference it.

5. **Roadmap document updated:**
   - Phase 6 status: "unblocked" (was "blocked").
   - Phase 7 status: "unblocked" (was "blocked").
   - Phase 8 status: "partially unblocked" (was "blocked").
   - Cross-assembly interface table updated with `IJSCompiler` and status column.
   - Recommended next steps updated — all prerequisite items marked done.
   - Estimated effort updated for remaining extraction work.

**Verification:**
- `Broiler.JavaScript.Core` compiles with zero errors.
- `Broiler.JavaScript.Clr` compiles with zero errors.
- `Broiler.JavaScript.Debugger` compiles with zero errors.
- All **641** tests in `Broiler.JavaScript.Core.Tests` pass.
- All **73** tests in `Broiler.JavaScript.Ast.Tests` pass.
- All **78** tests in `Broiler.JavaScript.Parser.Tests` pass.
- All **56** tests in `Broiler.JavaScript.Storage.Tests` pass.
- All **23** tests in `Broiler.JavaScript.Debugger.Tests` pass.
- **Total: 871 tests across 5 stable test projects, all passing.**
- Note: 2 of 29 Clr.Tests are flaky due to module initializer timing
  (`ClrExpressionBuilderTests` — pre-existing, not related to this change).

**Lessons learned:**
- The `JSClassGenerator` was already multi-assembly compatible by design; the
  Network assembly had been using it for multi-assembly registration. The
  perceived blocker was primarily about documentation and cleanup, not about
  missing functionality.
- The `IJSCompiler` interface was already implemented and wired in, but the
  roadmap document didn't document it. This reduced the Phase 7 effort estimate
  from "High" to "Medium" (the interface design work is done; only file moves
  remain).
- The circular dependency concern for `JSModuleContext` was a documentation gap.
  The upward-dependency pattern was already cleanly implemented — `JSContext`
  has no reverse references to `JSModuleContext`.

### Phase 6 — BuiltIns Extraction ✅ (Partial)

**Status:** Partial — first batch of built-in types extracted

**Date:** 2026-03-20

**What was done:**

1. Created `Broiler.JavaScript.BuiltIns` assembly at
   `Broiler.JavaScript/Broiler.JavaScript.BuiltIns/`.
2. Added `AdditionalRegistrations` delegate to `DefaultBuiltInRegistry` to
   enable satellite assemblies to contribute built-in type registrations without
   circular dependencies.
3. Moved **6 source files** from `Broiler.JavaScript.Core`:
   - From `Core/Weak/`: `WeakRef.cs` (contains `JSWeakRef` and
     `JSFinalizationRegistry`)
   - From `Core/Events/`: `EventTarget.cs`, `Event.cs`, `CustomEvent.cs`,
     `DomEventHandler.cs`
4. All moved types retain their original namespaces
   (`Broiler.JavaScript.Core.Core.Weak`, `Broiler.JavaScript.Core.Core.Events`).
5. The BuiltIns assembly references Core via `<ProjectReference>` (upward
   dependency, same pattern as Debugger, Clr, Modules, Compiler).
6. Created `Names.cs` with `[JSRegistrationGenerator]` — the JSClassGenerator
   source generator produces `RegisterAll` for the moved types.
7. Created `BuiltInsAssemblyInitializer.cs` with `[ModuleInitializer]` that
   wires `AdditionalRegistrations` so extracted types are automatically
   registered when a `JSContext` is created.
8. Updated `Broiler.slnx` to include both `Broiler.JavaScript.BuiltIns` and
   `Broiler.JavaScript.BuiltIns.Tests`.

**Types that could NOT be extracted (coupling analysis — updated 2026-03-21):**

Three categories of built-in types remain in Core due to coupling:

- **Internal field access (partially resolved by Phase 10 API changes):**
  DataView (`JSArrayBuffer.buffer`), JSJSON (`JSFunction.f` → resolved via
  `JSFunction.Delegate`; `JSNumber.value` → resolved via `JSNumber.NumberValue`;
  `JSString.value` → resolved via `JSString.StringValue`), JSReflect
  (`JSObject.IsExtensible`). Phase 10 API changes resolved most visibility
  issues, but DataView and JSReflect still access internal Core fields.
- **Protected internal overrides:** JSProxy overrides `protected internal`
  `GetValue`/`SetValue` methods on `JSObject`. Moving to another assembly
  changes access modifier semantics.
- **Deep structural coupling (primary blocker):** The remaining types cannot be
  extracted to BuiltIns because Core and/or Compiler assemblies depend on them
  directly, which would create a reverse dependency (Core/Compiler → BuiltIns):
  - `JSDisposableStack`: Compiler emits expressions using `JSDisposableStack`
    type for `using` declarations (3 files in Compiler)
  - `JSIntl`/`JSIntlDateTimeFormat`: `JSGlobal.cs` (type registration) and
    `JSDatePrototype.cs` (date formatting) depend on these types
  - `JSDecimal`: `JSMath.cs` (6 methods), `JSBigIntBuilder.cs` (expression
    builder), and `FastCompiler.VisitLiteral.cs` reference this type
  - `JSArray` (13 type checks), `JSString` (8 type checks), `JSNumber` (static
    property access from JSMath/JSDatePrototype), `JSError` (inheritance chain,
    type checks from JSException), `JSPromise` (stored in JSContext), `JSRegExp`
    (7 type checks from JSStringPrototype), `JSBigInt`/`JSDate`/`JSMap`/`JSSet`
    (type checks in JSGlobal)

  > **Note:** These coupling issues are not access-modifier problems (Phase 10
  > resolved those). They are **dependency-direction** problems: the types are
  > used by Core/Compiler code, so moving them to BuiltIns would require
  > Core/Compiler → BuiltIns references, violating the unidirectional dependency
  > graph. Resolution requires refactoring Core/Compiler to use factory delegates
  > or interface abstractions, similar to the `CoreScript` pattern in Phase 9d.

**Registration pattern:**

`DefaultBuiltInRegistry` was enhanced with a static `AdditionalRegistrations`
delegate property. The module initializer in `BuiltInsAssemblyInitializer`
appends to this delegate, supporting multiple satellite assemblies:

```csharp
// In DefaultBuiltInRegistry
public static Action<JSContext> AdditionalRegistrations { get; set; }

public void Register(JSContext context)
{
    context.RegisterGeneratedClasses();      // Core types
    AdditionalRegistrations?.Invoke(context); // Satellite types
    SetupIteratorPrototypeChain(context);
    // ...
}
```

**Test project (2026-03-20):**

9. Created `Broiler.JavaScript.BuiltIns.Tests` test project at
   `Broiler.JavaScript/Broiler.JavaScript.BuiltIns.Tests/`.
10. Added **16 assembly-specific tests** covering:
    - `WeakRefTests` — JS registration check, construction, deref, C# API.
    - `FinalizationRegistryTests` — JS registration check, callback requirement,
      valid construction.
    - `EventTargetTests` — JS registration check, construction, dispatchEvent,
      event type checking.
    - `EventTests` — C# API for Event.Create factory, type, bubbles, cancelable.
    - `AdditionalRegistrationsTests` — verifies delegate is set, WeakRef +
      EventTarget + FinalizationRegistry available in JSContext.
11. Test project bootstrap forces assembly loading for Clr, Compiler, and
    BuiltIns assemblies via `RuntimeHelpers.RunModuleConstructor`.
12. All 16 tests pass.

**Verification:**
- `Broiler.JavaScript.BuiltIns` compiles with zero errors.
- `Broiler.JavaScript.Core` compiles with zero errors.
- All **641** tests in `Broiler.JavaScript.Core.Tests` pass.
- All **16** tests in `Broiler.JavaScript.BuiltIns.Tests` pass.
- **Total: 934 tests across 9 test projects, all passing.**

---

### Phase 7 — Compiler Extraction (2026-03-20)

**Status: Complete**

Extracted `FastCompiler` and all 40+ partial classes, `FastFunctionScope`, and
`StrictModeExtensions` into a new **`Broiler.JavaScript.Compiler`** assembly.

**Architecture decisions:**
- `DefaultJSCompiler` stays in Core with a **delegate registration pattern**
  (mirrors `ClrProxyBuilder`).  The Compiler assembly's module initializer
  calls `DefaultJSCompiler.Register(...)` to wire in the `FastCompiler`
  pipeline.
- `IJSCompiler` interface remains in Core — consumers depend only on the
  interface.
- `InternalsVisibleTo("Broiler.JavaScript.Compiler")` added to Core because
  `FastCompiler` accesses internal builder methods
  (`CallStackItemBuilder`, `StrictModeExtensions`, etc.).
- Test projects use `RuntimeHelpers.RunModuleConstructor(typeof(FastCompiler).Module.ModuleHandle)`
  in their bootstrap to ensure the Compiler assembly's module initializer runs.

**Files moved:**
- `FastCompiler.cs` + 35 partial files (`FastCompiler.Visit*.cs`, etc.)
- `FastFunctionScope.cs`
- `StrictModeExtensions.cs`

**Files added:**
- `Broiler.JavaScript.Compiler/CompilerAssemblyInitializer.cs`
- `Broiler.JavaScript.Compiler/GlobalUsings.cs`
- `Broiler.JavaScript.Compiler/Broiler.JavaScript.Compiler.csproj`

**Files remaining in Core:**
- `IJSCompiler.cs` (interface contract)
- `DefaultJSCompiler.cs` (delegate-based dispatcher)

**Test results:**
- 9 new assembly-specific tests in `Broiler.JavaScript.Compiler.Tests`
- All **641** Core tests pass.
- All **29** Clr tests pass.
- All **23** Debugger tests pass.

### Phase 8 — Modules Extraction (2026-03-20)

**Status: Complete**

Extracted `JSModuleContext`, `JSModule`, and `ModuleCache` into a new
**`Broiler.JavaScript.Modules`** assembly.

**Architecture decisions:**
- Follows the upward-dependency pattern: Modules references Core, not
  vice-versa.
- `ClrModuleProvider` static property moved from `JSModuleContext` to
  `JSContext` so the Clr assembly can set it without referencing Modules.
- `JSContext.WaitTask` changed from `internal` to `public` so
  `JSModuleContext` can access it across the assembly boundary.
- `Names.cs` with `[JSRegistrationGenerator]` enables JSModule source
  generation in the new assembly (multi-assembly generator pattern).
- `IJSModuleResolver`, `ExportAttribute`, `DefaultExportAttribute` remain
  in Core as contract/attribute types.

**Files moved:**
- `JSModuleContext.cs`
- `JSModule.cs`
- `ModuleCache.cs`

**Files added:**
- `Broiler.JavaScript.Modules/Names.cs`
- `Broiler.JavaScript.Modules/GlobalUsings.cs`
- `Broiler.JavaScript.Modules/Broiler.JavaScript.Modules.csproj`

**Test results:**
- 9 new assembly-specific tests in `Broiler.JavaScript.Modules.Tests`
- All **641** Core tests pass.

### Updated Test Matrix (2026-03-20)

| Assembly | Tests | Status |
|----------|-------|--------|
| Broiler.JavaScript.Ast.Tests | 73 | ✅ Pass |
| Broiler.JavaScript.Parser.Tests | 78 | ✅ Pass |
| Broiler.JavaScript.Storage.Tests | 76 | ✅ Pass |
| Broiler.JavaScript.Core.Tests | 641 | ✅ Pass |
| Broiler.JavaScript.Debugger.Tests | 23 | ✅ Pass |
| Broiler.JavaScript.Clr.Tests | 29 | ✅ Pass |
| Broiler.JavaScript.Compiler.Tests | 9 | ✅ Pass |
| Broiler.JavaScript.Modules.Tests | 9 | ✅ Pass |
| Broiler.JavaScript.BuiltIns.Tests | 16 | ✅ Pass |
| Broiler.JavaScript.Runtime.Tests | 20 | ✅ Pass |
| **Total** | **974** | **✅ All Pass** |

### Continued Implementation Progress (2026-03-20, Phase 3–5)

#### Phase 3 — Storage Extraction (Continued)

**What was done:**

1. **Moved `JSPropertyAttributes` enum** from
   `Broiler.JavaScript.Core/Core/Storage/JSProperty.cs` into the Storage assembly
   at `Broiler.JavaScript.Storage/JSPropertyAttributes.cs`.
   - Namespace changed from `Broiler.JavaScript.Core.Core.Storage` to
     `Broiler.JavaScript.Storage`.
   - Core already has `global using Broiler.JavaScript.Storage;` so existing
     Core code compiles without changes.
   - Added `using Broiler.JavaScript.Storage;` to Clr and Modules.Tests files
     that reference the enum.

2. **Created property contract interfaces in Ast:**
   - `IPropertyValue` — marker interface for types storable as property values.
   - `IPropertyAccessor : IPropertyValue` — marker interface for getter/setter
     types.
   - File: `Broiler.JavaScript.Ast/IPropertyContracts.cs`.

3. **Implemented interfaces on Core types:**
   - `JSValue` now implements `IPropertyValue`.
   - `JSFunction` now implements `IPropertyAccessor`.
   - These interfaces prepare for moving `JSProperty` struct to Storage in a
     future iteration by allowing its `value`/`get`/`set` fields to reference
     interface types from Ast instead of concrete Core types.

4. **Added 20 tests** for `JSPropertyAttributes` to `Storage.Tests`:
   - Bit flag values, shortcut combinations, bitwise operations, default value.
   - Storage.Tests now has **76 tests** (was 56).

**Remaining Phase 3 blockers (JSProperty/PropertySequence/ElementArray):**

The structs `JSProperty`, `PropertySequence`, and `ElementArray` remain in Core
because their fields directly reference Core types (`JSValue`, `JSFunction`,
`JSFunctionDelegate`). Moving them to Storage requires converting these fields
to the new interface types (`IPropertyValue`, `IPropertyAccessor`). This
conversion is deferred because:

- Changing struct fields from concrete types to interfaces changes the public
  API surface (consumers must cast back to concrete types).
- `PropertySequence` depends on `JSContext.NewTypeError`, `KeyStrings.GetName`,
  `JSObject` (for `ValueEnumerator`).
- `ElementArray` depends on `JSMath.RandomNumber`, `Comparison<JSValue>`.
- These dependencies will be resolved when the Runtime assembly absorbs the
  value type system (Phase 5 full implementation).

#### Phase 4 — Debugger Decoupling (Verification)

**Status:** Previously completed. Verified during this session.

- No `InternalsVisibleTo` attribute for `Broiler.JavaScript.Debugger` exists in
  `AssemblyInfo.cs`.
- Debugger assembly interacts with Core via public API only.
- `Debugger.Tests` has **23 tests** covering `IDebugger` contract, script parsed
  notifications, exception reporting, and debugger attachment/detachment.

#### Phase 5 — Runtime Assembly (Preparation)

**What was done:**

1. **Created `Broiler.JavaScript.Runtime` project** at
   `Broiler.JavaScript/Broiler.JavaScript.Runtime/`.
   - Depends on: Ast, Storage.
   - Added to `Broiler.slnx`.

2. **Moved `IJSModuleResolver` interface** from Core to Runtime:
   - File: `Broiler.JavaScript.Runtime/IJSModuleResolver.cs`.
   - Namespace preserved as `Broiler.JavaScript.Core.Core.Module` for backward
     compatibility.
   - Core's `AssemblyInfo.cs` now has `[assembly: TypeForwardedTo(typeof(IJSModuleResolver))]`
     so downstream assemblies continue to resolve the type through Core.
   - Core's `IJSModuleResolver.cs` replaced with a comment pointing to Runtime.

3. **Core references Runtime** (`<ProjectReference>` added to Core.csproj).
   - No circular dependency: Runtime → Storage → Ast; Core → Runtime.

**Contracts awaiting extraction to Runtime:**

| Contract | Current Location | Blocked By |
|----------|-----------------|------------|
| `IBuiltInRegistry` | Core | References `JSContext` |
| `IClrInterop` | Core | References `JSValue` |
| `IDebugger` | Core | References `JSValue` |
| `IJSCompiler` | Core | References `JSFunctionDelegate`, `ICodeCache` |
| `IJSModuleResolver` | **Runtime** ✅ | — |
| `ExportAttribute` | **Runtime** ✅ | — |
| `DefaultExportAttribute` | **Runtime** ✅ | — |
| `CancellableDisposableAction` | **Runtime** ✅ | — |

These interfaces will move to Runtime once `JSValue`, `JSContext`, and
`JSFunctionDelegate` are extracted from Core to Runtime (full Phase 5).

### InternalsVisibleTo Audit and API Surface Changes (2026-03-20)

**Status:** Audit complete — 3 internal APIs made public

**Date:** 2026-03-20

**What was done:**

1. **`BasePrototypeObject` setter made public** — Changed from
   `internal virtual` (on `JSValue`) and `internal override` (on `JSObject`)
   to `public virtual`/`public override`. This eliminates 5 internal-access
   sites in the Clr assembly (`ClrProxy.cs` ×2, `ClrModule.cs` ×1,
   `ClrType.cs` ×2). The property is write-only (no getter) and controls
   prototype chain assignment — a safe and intentional API.

2. **`KeyStringsBuilder` class made public** — Changed from `internal class`
   to `public class` in `LinqExpressions/KeyStringsBuilder.cs`. This
   eliminates 2 internal-access sites in the Compiler assembly
   (`FastCompiler.KeyOfName.cs`). The class provides expression tree building
   helpers for `KeyString` field lookups.

3. **`JSSpreadValueBuilder` class made public** — Changed from `internal class`
   to `public class` in `LinqExpressions/ClrSpreadExpression.cs`. This
   eliminates 2 internal-access sites in the Compiler assembly
   (`FastCompiler.VisitCallExpression.cs`). The class provides expression tree
   building for spread arguments.

**Audit results — remaining internal accesses:**

| Assembly | Internal Member | Location | Reason Cannot Remove |
|----------|----------------|----------|---------------------|
| Clr | `JSString.value`, `JSNumber.value`, `JSDate.value` (fields) | ClrProxy.cs, ClrModule.cs | Primitive value extraction; needs public read-only properties |
| Clr | `NumberParser.CoerceToNumber` | ClrProxy.cs | Internal method on public class; make public when safe |
| Clr | `Type.GetElementTypeOrGeneric` (ext method) | ClrType.cs, JSPropertyInfo.cs | Internal extension class; make public |
| Clr | `ArgumentsBuilder.refType` (field) | ClrType.cs, ClrTypeExtensions.cs | Internal builder member |
| Clr | `JSFunction(delegate,type)` constructor | ClrType.cs | Internal constructor; consider `protected` |
| Clr | `ToJSValue` (ext method) | JSFieldInfo.cs | Internal extension; make public |
| Compiler | Remaining internal builder methods | Multiple FastCompiler partial files | Internal extension methods (`ToJSValue`, `CallExpression`, `ConvertToNumber`, `ConvertToString`, `ConvertToInteger`, `ConvertToJSValue`) and `JSVariable.ValueExpression`; making extension classes public or refactoring to instance methods needed |
| Runtime | Dynamic assembly internals | Used by IL generation | Required for `DynamicMethod` generation |

**Verification:**
- All 10 production assemblies compile with zero errors.
- All **962** tests pass across 10 test projects.

### InternalsVisibleTo Bridge Reduction (2026-03-20, continued)

**Status:** 7 additional APIs made public; 1 new public property added

**Date:** 2026-03-20

**What was done:**

1. **`JSFunction.Delegate` public property added** — New `public JSFunctionDelegate
   Delegate { get; set; }` property provides controlled access to the internal
   `f` field. `ClrType` constructor updated to use `Delegate` instead of `f`
   (2 sites: line 257 initial assignment, line 271 constructor delegate wiring).

2. **`CallStackItemBuilder` made public** — Changed from `internal static class`
   to `public static class` in `LinqExpressions/CallStackItemBuilder.cs`.
   Eliminates 3 Compiler internal accesses (`FastCompiler.VisitBlock.cs`,
   `FastCompiler.VisitProgram.cs`, `FastCompiler.CreateFunction.cs`).

3. **`StringSpanBuilder.New()` methods made public** — Changed both `New()`
   overloads from `internal static` to `public static`. Eliminates 9+ Compiler
   internal accesses across multiple `FastCompiler` files.

4. **`NumberParser` class made public** — Changed from `internal static class`
   to `public static class`. `TryCoerceToUInt32` method changed from
   `internal static` to `public static`. Other methods remain `internal`.
   Eliminates 2 Compiler internal accesses (`FastCompiler.VisitMemberExpression.cs`,
   `FastCompiler.VisitObjectLiteral.cs`).

5. **`JSValue.SetValue(uint, JSValue, JSValue, bool)` made public** — Changed
   from `internal protected virtual` to `public virtual`. Cascaded to 14
   overrides across `JSObject`, `JSArray`, `JSProxy`, `ClrProxy`, and all
   typed arrays (`JSFloat16Array`, `JSFloat32Array`, `JSFloat64Array`,
   `JSInt8Array`, `JSInt16Array`, `JSInt32Array`, `JSUInt8Array`,
   `JSUInt16Array`, `JSUInt32Array`, `JSUint8ClampedArray`). Matches
   `GetValue(uint, ...)` which was already public.

6. **Detailed Clr audit performed** — Attempted full removal of
   `InternalsVisibleTo("Broiler.JavaScript.Clr")`. Found 34 remaining internal
   accesses across 6 categories (value fields, methods, extension methods,
   builder members, constructors). Documented full findings in
   "Remaining Work" section with resolution approaches.

7. **CI workflow added** — `.github/workflows/ci.yml` created with multi-platform
   test matrix (Ubuntu, Windows, macOS) covering all 9 test projects.

**Summary of `InternalsVisibleTo` bridge status:**

| Assembly | Before | After | Delta |
|----------|--------|-------|-------|
| Debugger | 0 internal accesses | 0 | — (already resolved) |
| Clr | ~40 internal accesses | ~34 internal accesses | −6 (Delegate, BasePrototype, SetValue) |
| Compiler | ~20 internal accesses | ~6 internal accesses | −14 (CallStackItemBuilder, StringSpanBuilder, NumberParser, KeyStringsBuilder, JSSpreadValueBuilder, ArgumentsBuilder.refType, JSValueBuilder.StaticEquals, JSBigIntBuilder.New, JSDecimalBuilder.New) |

**Verification:**
- All 10 production assemblies compile with zero errors.
- All **962** tests pass across 10 test projects.

### Remaining Work

**`InternalsVisibleTo` bridge resolution:**

- [x] Remove `InternalsVisibleTo` for Debugger assembly — ✅ all APIs made public
- [x] Make `BasePrototypeObject` setter public — ✅ reduces Clr bridge (5 sites)
- [x] Make `KeyStringsBuilder` public — ✅ reduces Compiler bridge (2 sites)
- [x] Make `JSSpreadValueBuilder` public — ✅ reduces Compiler bridge (2 sites)
- [x] Add `JSFunction.Delegate` public property — ✅ resolves `JSFunction.f` field
  access from Clr (ClrType.cs constructor, 2 sites)
- [x] Make `CallStackItemBuilder` public — ✅ reduces Compiler bridge (3 sites in
  `FastCompiler.VisitBlock.cs`, `FastCompiler.VisitProgram.cs`,
  `FastCompiler.CreateFunction.cs`)
- [x] Make `StringSpanBuilder.New()` methods public — ✅ reduces Compiler bridge
  (9+ sites across FastCompiler files)
- [x] Make `NumberParser` and `TryCoerceToUInt32` public — ✅ reduces Compiler
  bridge (2 sites in `FastCompiler.VisitMemberExpression.cs` and
  `FastCompiler.VisitObjectLiteral.cs`)
- [x] Make `JSValue.SetValue(uint, ...)` public — ✅ cascaded to all 14 overrides;
  matches `GetValue(uint, ...)` which was already public. Reduces Clr bridge
  (`ClrProxy.SetValue` override)
- [x] Resolve remaining Clr internal accesses — ✅ **all resolved** (2026-03-20).
  30 compilation errors fixed; `InternalsVisibleTo("Broiler.JavaScript.Clr")`
  entry removed. See Clr resolution details below.
- [x] Resolve remaining Compiler internal accesses — ✅ **all resolved**
  (2026-03-20). 44 compilation errors fixed;
  `InternalsVisibleTo("Broiler.JavaScript.Compiler")` entry removed. See
  Compiler resolution details below.
- [x] Remove `InternalsVisibleTo("Broiler.JavaScript.Tests")` — ✅ legacy entry
  removed (project does not exist in repository)

**Clr `InternalsVisibleTo` resolution (2026-03-20):**

All remaining Clr internal accesses were resolved by making APIs public:

| Category | Resolution | Details |
|----------|-----------|---------|
| `JSString.value` field | Added `JSString.StringValue` public property | `ClrProxy.cs` updated to use `.StringValue` |
| `JSNumber.value` field | Added `JSNumber.NumberValue` public property | `ClrProxy.cs` updated to use `.NumberValue` |
| `JSDate.value` field | Use existing `JSDate.Value` public property | `ClrModule.cs` updated to use `.Value` |
| `NumberParser.CoerceToNumber` | Made method `public` | Was `internal static` |
| `TypeExtensions` class + methods | Made class and all methods `public` | Includes `GetElementTypeOrGeneric`, `Property`, `PublicField`, `InternalField`, `PublicIndex`, `IndexProperty` |
| `ExpressionHelper` class | Made class `public` | Contains `ToJSValue` extension used by `JSFieldInfo.cs` |
| `JSFunction(delegate,type)` ctor | Made constructor `public` | Was `internal`; `ClrType.cs` uses `new JSFunction(delegate, this)` |

**Compiler `InternalsVisibleTo` resolution (2026-03-20):**

All remaining Compiler internal accesses were resolved by making APIs public:

| Category | Resolution | Details |
|----------|-----------|---------|
| `ExpressionHelper` class | Made class `public` | 22 `ToJSValue` errors resolved |
| `NewLambdaExpression` class | Made class `public` | 8 `CallExpression` errors resolved |
| `ListOfExpressionsExtensions` class | Made class and all `ConvertTo*` methods `public` | 8 `ConvertToInteger`/`ConvertToNumber`/`ConvertToString`/`ConvertToJSValue` errors resolved |
| `JSVariable.ValueExpression` | Made method `public` | 6 errors resolved |

**Phase completion (next actions):**

- [x] Phase 3 continued — move `JSProperty`, `PropertySequence`, `ElementArray`
  to Storage by converting fields to `IPropertyValue`/`IPropertyAccessor`
  ✅ (2026-03-21) — `PropertySequence`/`ElementArray` method signatures
  converted to interface types; `PropertyValueEnumerator` extracted to Core;
  `PropertySequenceCoreExtensions` handles `JSFunctionDelegate` Put overload.
- [x] Phase 6 continued — extract additional built-in types to BuiltIns assembly
  ✅ Partially complete (Events, Weak extracted). Remaining types structurally
  blocked: Disposable (Compiler depends on `JSDisposableStack`), Intl (Core's
  `JSGlobal`/`JSDatePrototype` depend on `JSIntl`/`JSIntlDateTimeFormat`),
  Decimal (Core's `JSMath`/`JSBigIntBuilder` + Compiler depend on `JSDecimal`).
  All remaining candidates create reverse dependency (Core/Compiler → BuiltIns)
  if moved. See Section 27 for detailed analysis.
- [x] Phase 9a — move `KeyString`/`KeyStrings` to Runtime ✅ — moved to Storage;
  move `JSProperty`, `PropertySequence`, `ElementArray` to Storage ✅
- [x] Phase 9b — move `JSValue`, `Arguments`, `PropertyKey`, `JSFunctionDelegate`,
  `IElementEnumerator` to Runtime ✅; concrete types (`JSObject`, `JSFunction`,
  `JSContext`) deferred per Section 26
- [x] Phase 10 — resolve all `InternalsVisibleTo` bridges, create meta-package,
  update downstream consumers ✅ (2026-03-20)

**Infrastructure:**

- [x] Update downstream consumers (Broiler.App, Broiler.Cli) to reference
  `Broiler.JavaScript.All` meta-package ✅ (2026-03-20)
- [x] Cross-platform CI build/test matrix (Linux/macOS/Windows) — ✅ created
  `.github/workflows/ci.yml`; covers all 10 test projects with
  `coverlet` coverage collection (2026-03-20)
- [x] `Broiler.JavaScript.Runtime.Tests` project created — ✅ 15 tests covering
  `IJSModuleResolver` contract and `ExportAttribute`/`DefaultExportAttribute`
- [x] Create `Broiler.JavaScript.All` meta-package for convenience references ✅
  (2026-03-20)
- [x] Add missing extracted assemblies to solution file (Clr, Compiler, Modules,
  BuiltIns, their test projects, Runtime.Tests, All) ✅ (2026-03-20)
- [x] Integrate `coverlet` coverage measurement into CI ✅ (2026-03-20) —
  `coverlet.collector` v6.0.2 added to all 10 test projects; CI collects
  `XPlat Code Coverage` data on each test run

---

## 10. Phase 9 — Runtime Extraction (Planning)

### Overview

Phase 9 is the most complex remaining extraction: moving the core execution types
(`JSValue`, `JSContext`, `JSObject`, `JSFunction`, `Arguments`, `KeyString`,
`CoreScript`, `Bootstrap`, etc.) from `Broiler.JavaScript.Core` into
`Broiler.JavaScript.Runtime`.

### Current State

The `Broiler.JavaScript.Runtime` assembly now contains the core value type
system (`JSValue`, `Arguments`, `PropertyKey`, `JSFunctionDelegate`,
`IElementEnumerator`), interface abstractions (`IJSPrototype`, `IJSSymbol`),
contract types (`IJSModuleResolver`, `ExportAttribute`, `DefaultExportAttribute`),
and utility types (`CancellableDisposableAction`, `ObjectStatus`,
`StringExtensions`). Phase 9b is complete.

### Circular Dependency Challenge

The primary blocker is a circular dependency between Runtime and Storage:

- **Runtime → Storage:** `JSObject` uses `SAUint32Map<JSProperty>`,
  `PropertySequence`, `StringMap<JSProperty>` for property storage.
- **Storage → Runtime:** `JSProperty` references `JSValue`, `JSFunction`,
  `KeyString`. `PropertySequence` references `JSContext`, `JSObject`.

### Resolution Strategy

1. **Extract `KeyString` to Ast** — `KeyString` is a property name interning
   struct with no runtime dependencies. Move it to Ast (or a `Primitives`
   assembly) so both Runtime and Storage can reference it.

2. **Convert `JSProperty` fields to interfaces** — Use the existing
   `IPropertyValue` and `IPropertyAccessor` interfaces (already defined in Ast)
   to replace `JSValue` and `JSFunction` references in `JSProperty` fields.
   This allows `JSProperty` to live in Storage without depending on Runtime.

3. **Two-phase extraction:**
   - **Phase 9a:** Move `KeyString`, `KeyStrings` to Ast. Move `JSProperty`,
     `PropertySequence`, `ElementArray` to Storage (with interface-typed fields).
   - **Phase 9b:** Move `JSValue`, `JSObject`, `JSFunction`, `JSContext`,
     `Arguments`, `CoreScript`, `Bootstrap` to Runtime.

### Phase 9 Task Checklist

**Phase 9a — KeyString + Property Types:**

- [x] Move `KeyString` struct to `Broiler.JavaScript.Storage` (originally planned
  for Ast; moved to Storage to avoid circular dependency). ✅ (2026-03-20)
- [x] Move `KeyStrings` static class to `Broiler.JavaScript.Storage`. ✅ (2026-03-20)
- [x] Move `KeyType` enum to `Broiler.JavaScript.Storage`. ✅ (2026-03-20)
- [x] Add `TypeForwardedTo` attributes in Core for `KeyString`/`KeyStrings`/`KeyType`. ✅ (2026-03-20)
- [x] Convert `JSProperty.value` field from `JSValue` to `IPropertyValue`. ✅ (2026-03-20)
- [x] Convert `JSProperty.get`/`JSProperty.set` fields from `JSFunction` to
  `IPropertyAccessor`. ✅ (2026-03-20)
- [x] Move `JSProperty` to Storage. ✅ (2026-03-20)
- [x] Create `JSPropertyFactory` in Core for factory methods requiring `JSFunction`. ✅ (2026-03-20)
- [x] Add `TypeForwardedTo` attribute in Core for `JSProperty`. ✅ (2026-03-20)
- [x] Add explicit casts at ~30 field access sites across Core and Debugger. ✅ (2026-03-20)
- [x] Run full test suite (998 tests) — all pass. ✅ (2026-03-20)
- [x] Move `PropertySequence`, `ElementArray` to Storage. ✅ (2026-03-21)
  Interface-typed method parameters (`IPropertyValue`, `IPropertyAccessor`).
  `PropertyValueEnumerator` (formerly `PropertySequence.ValueEnumerator`)
  extracted to Core. `PropertySequenceCoreExtensions` provides `JSFunctionDelegate`
  `Put` overload and `[ModuleInitializer]` for `TypeErrorFactory` setup.
  `ElementArray.QuickSort` converted to `Comparison<IPropertyValue>` (was
  `Comparison<JSValue>`); `JSMath.RandomNumber()` replaced with
  `Random.Shared.NextDouble()`. `TypeForwardedTo` attributes added for
  `JSObjectProperty`, `PropertySequence`, `ElementArray`, `Updater<,>`.

**Phase 9b — Value Type System ✅ (2026-03-21):**

- [x] Create `IJSPrototype` interface in Runtime (abstraction over prototype chain). ✅ (2026-03-21)
- [x] Create `IJSSymbol` interface in Runtime (abstraction over symbol values). ✅ (2026-03-21)
- [x] Move `JSValue` to `Broiler.JavaScript.Runtime`. ✅ (2026-03-21)
- [x] Move `Arguments` to Runtime. ✅ (2026-03-21)
- [x] Move `PropertyKey` to Runtime. ✅ (2026-03-21)
- [x] Move `JSFunctionDelegate` to Runtime. ✅ (2026-03-21)
- [x] Move `IElementEnumerator` to Runtime. ✅ (2026-03-21)
- [x] Add `InvokePropertyGetter` and `CreatePrototypeObject` factory delegates to JSValue. ✅ (2026-03-21)
- [x] Wire all factory delegates via `[ModuleInitializer]` in `JSValueCoreExtensions`. ✅ (2026-03-21)
- [x] Add `TypeForwardedTo` attributes in Core for all moved types. ✅ (2026-03-21)
- [x] Update `InternalsVisibleTo` in Runtime for all satellite assemblies. ✅ (2026-03-21)
- [x] Fix JSValueBuilder reflection for changed field types (`IJSPrototype`, `IJSSymbol`). ✅ (2026-03-21)
- [x] Run full test suite — all 998 tests pass. ✅ (2026-03-21)
- [ ] Move `JSObject`, `JSFunction`, `JSContext` to Runtime (deferred — see
  Section 26 for architectural assessment; Section 29.5 for action plan).
- [x] Move `CoreScript` to Runtime ✅ (2026-03-21, Phase 9d — via factory delegates).
- [x] Move contract interfaces (`IClrInterop`, `IDebugger`, `IJSCompiler`) to
  Runtime ✅ (2026-03-21). `ICodeCache`, `JSCode`, `JSCodeCompiler` also moved.
  `IBuiltInRegistry` resolved in Phase 9c+ via `IJSContext` abstraction.
- [x] Move `IBuiltInRegistry` to Runtime ✅ (2026-03-21, Phase 9c+ — via `IJSContext` interface).
- [ ] Update downstream consumer docs (Section 11) — tracked in Section 29.4.

### Contracts to Move

| Contract | Current Location | Target | Status |
|----------|-----------------|--------|--------|
| `IBuiltInRegistry` | **Runtime** ✅ | — | ✅ Moved (2026-03-21, Phase 9c+ via `IJSContext`) |
| `IClrInterop` | **Runtime** ✅ | — | ✅ Moved (2026-03-21) |
| `IDebugger` | **Runtime** ✅ | — | ✅ Moved (2026-03-21) |
| `IJSCompiler` | **Runtime** ✅ | — | ✅ Moved (2026-03-21) |
| `ICodeCache` | **Runtime** ✅ | — | ✅ Moved (2026-03-21) |
| `JSCode` | **Runtime** ✅ | — | ✅ Moved (2026-03-21) |
| `JSCodeCompiler` | **Runtime** ✅ | — | ✅ Moved (2026-03-21) |
| `IJSModuleResolver` | **Runtime** ✅ | — | Already moved |

### Estimated Effort

- **Phase 9a (KeyString + property types):** ✅ **Complete** — all storage types
  moved to Storage assembly.
- **Phase 9b (JSValue + Arguments + PropertyKey):** ✅ **Complete** — core value
  types moved to Runtime. Factory delegate pattern used for cross-assembly
  construction. Interface abstractions (`IJSPrototype`, `IJSSymbol`) created.
- **Phase 9c (contract interfaces):** ✅ **Partial** — `IDebugger`, `IClrInterop`,
  `IJSCompiler`, `ICodeCache`/`JSCode`/`JSCodeCompiler` moved to Runtime.
  `IBuiltInRegistry` blocked by `JSContext`. `TypeForwardedTo` attributes added.
- **Phase 9c (JSObject + JSFunction + JSContext):** High — these types are
  referenced by virtually every file in the engine (~500+ files). Requires
  extensive factory delegate work and interface abstraction.

### Risk Assessment

- **API breakage:** `JSValue` and `JSContext` are the primary public API types.
  Any namespace change breaks all downstream consumers. Mitigation: use
  `TypeForwardedTo` and `global using` aliases.
- **Performance:** No runtime cost expected (same AppDomain, same JIT).
- **Build time:** Significant increase in initial migration effort (500+ files
  reference these types).

---

## 11. Downstream Consumer Migration Guide

### Current State

Downstream consumers currently reference `Broiler.JavaScript.Core` as a single
dependency, which transitively includes all extracted assemblies:

| Consumer | Current Reference | Target References |
|----------|------------------|-------------------|
| `Broiler.App` (WPF) | `Broiler.JavaScript.Core` | `Core` + `Clr` + `BuiltIns` + `Compiler` + `Modules` + `Debugger` |
| `Broiler.Cli` | `Broiler.JavaScript.Core` | Same as above |
| `Broiler.DevConsole` | `Broiler.JavaScript.Core` | Same as above |
| `Broiler.JavaScript` (CLI) | `Broiler.JavaScript.Core` | All assemblies |

### Migration Steps

**Phase A — Transitive compatibility (current):**
No changes needed. Downstream consumers reference `Core`, which has
`<ProjectReference>` entries for Ast, Parser, and Storage (downward dependencies).
Clr, Compiler, Debugger, Modules, and BuiltIns use module initializers that
auto-register when their assemblies are loaded.

**Phase B — Explicit satellite references:**
Add explicit `<ProjectReference>` entries for satellite assemblies that use
module initializers (Clr, Compiler, Modules, BuiltIns). This ensures the
assemblies are included in the output and their initializers run:

```xml
<!-- In Broiler.App.csproj / Broiler.Cli.csproj -->
<ItemGroup>
  <ProjectReference Include="...\Broiler.JavaScript.Core.csproj" />
  <ProjectReference Include="...\Broiler.JavaScript.Clr.csproj" />
  <ProjectReference Include="...\Broiler.JavaScript.Compiler.csproj" />
  <ProjectReference Include="...\Broiler.JavaScript.Modules.csproj" />
  <ProjectReference Include="...\Broiler.JavaScript.BuiltIns.csproj" />
  <ProjectReference Include="...\Broiler.JavaScript.Debugger.csproj" />
</ItemGroup>
```

**Phase C — Meta-package:** ✅ **Complete** (2026-03-20)
`Broiler.JavaScript.All` meta-package created. It transitively includes all
engine assemblies (Core, Clr, Compiler, Modules, BuiltIns, Debugger).
Both `Broiler.App` and `Broiler.Cli` now reference the meta-package:

```xml
<ProjectReference Include="...\Broiler.JavaScript.All\Broiler.JavaScript.All.csproj" />
```

### Current Consumer Analysis

- `Broiler.App` and `Broiler.Cli` both reference `Broiler.JavaScript.All`,
  which transitively includes Core and all satellite assemblies (Clr, Compiler,
  BuiltIns, Modules, Debugger). Module initializers auto-register when loaded.
- `Broiler.JavaScript` (CLI REPL) now references Core, Clr, Modules, Network,
  and ExpressionCompiler directly. References, target framework, and namespaces
  updated to match the refactored assembly structure (2026-03-21).

---

## 12. CI/CD Configuration

### Workflow

A GitHub Actions CI workflow has been added at `.github/workflows/ci.yml` that
runs all 11 test projects on Ubuntu, Windows, and macOS with code coverage
collection via `coverlet`:

```yaml
# Trigger: push to main, pull requests to main
# Matrix: ubuntu-latest, windows-latest, macos-latest
# Steps: checkout (full depth) → setup .NET 8 → restore → build → test (10 projects)
# Coverage: --collect:"XPlat Code Coverage" via coverlet.collector on each test step
```

### Test Matrix

| Assembly | Test Project | Test Count |
|----------|-------------|------------|
| Core | `Broiler.JavaScript.Core.Tests` | 641 |
| Ast | `Broiler.JavaScript.Ast.Tests` | 73 |
| Parser | `Broiler.JavaScript.Parser.Tests` | 78 |
| Storage | `Broiler.JavaScript.Storage.Tests` | 100 |
| Debugger | `Broiler.JavaScript.Debugger.Tests` | 23 |
| Clr | `Broiler.JavaScript.Clr.Tests` | 29 |
| Compiler | `Broiler.JavaScript.Compiler.Tests` | 9 |
| Modules | `Broiler.JavaScript.Modules.Tests` | 9 |
| BuiltIns | `Broiler.JavaScript.BuiltIns.Tests` | 29 |
| Runtime | `Broiler.JavaScript.Runtime.Tests` | 20 |
| ModuleExtensions | `Broiler.JavaScript.ModuleExtensions.Tests` | 13 |
| **Total** | **11 projects** | **1024** |

### Coverage Configuration

All 11 test projects include `coverlet.collector` v6.0.2 as a package reference.
The CI workflow passes `--collect:"XPlat Code Coverage"` to each test step,
generating Cobertura XML coverage reports in the test results directory.

To run coverage locally:

```bash
dotnet test Broiler.JavaScript/Broiler.JavaScript.Runtime.Tests/ --collect:"XPlat Code Coverage"
```

---

### Continued Implementation Progress (2026-03-20, CI/Testing/Bridge Reduction)

**Date:** 2026-03-20

**What was done:**

1. **Created `Broiler.JavaScript.Runtime.Tests`** test project at
   `Broiler.JavaScript/Broiler.JavaScript.Runtime.Tests/`.
   - 8 assembly-specific tests covering the `IJSModuleResolver` interface
     contract: implementation verification, path resolution (found/not-found),
     async source loading (success/failure), nested path handling, completed
     task semantics, and multi-resolver independence.
   - Test project references only `Broiler.JavaScript.Runtime` (no Core dependency).
   - Added to `Broiler.slnx`.

2. **Created `.github/workflows/ci.yml`** — GitHub Actions CI workflow with
   multi-platform test matrix:
   - **Platforms:** Ubuntu, Windows, macOS.
   - **Steps:** Checkout → Setup .NET 8 → Restore → Build → Test (10 projects).
   - All 10 test projects run on each platform.

3. **Made 4 additional Compiler internal APIs public:**
   - `ArgumentsBuilder.refType` — `internal static readonly Type` → `public`
     (used by `FastCompiler.cs` for `Arguments` by-ref parameter expression).
   - `JSValueBuilder.StaticEquals` — `internal static MethodInfo` → `public`
     (used by `FastCompiler.VisitSwitchStatement.cs` for equality comparison).
   - `JSBigIntBuilder.New(string)` — `internal static` → `public`
     (used by `FastCompiler.VisitLiteral.cs` and
     `FastCompiler.VisitUnaryExpression.cs`).
   - `JSDecimalBuilder.New(string)` — `internal static` → `public`
     (used by `FastCompiler.VisitLiteral.cs`).

4. **Detailed Compiler `InternalsVisibleTo` audit performed:**
   - With bridge removed: 44 compilation errors across 4 error categories.
   - Remaining internal accesses identified: internal extension methods
     (`ToJSValue`, `CallExpression`, `ConvertToNumber`, `ConvertToString`,
     `ConvertToInteger`, `ConvertToJSValue`) and `JSVariable.ValueExpression`.
   - Bridge reduction: ~20 → ~6 internal accesses remaining.
   - Full removal requires making internal extension classes public, which is a
     larger API surface change deferred to a future session.

5. **Documentation updated:**
   - Project Inventory table — added `Runtime.Tests`.
   - Testing Requirements table — updated Runtime from "Future" to "✅ 8 tests".
   - Test Matrix — updated from 954 to 962 tests across 10 projects.
   - InternalsVisibleTo Status — updated Compiler entry with specific remaining
     internal members and newly resolved bridges.
   - CI/CD Section 12 — updated test matrix with Runtime row.
   - Remaining Work — updated Compiler audit with specific member names and
     error count; added Runtime.Tests completion.

**Verification:**
- All 10 production assemblies compile with zero errors.
- All **962** tests pass across 10 test projects:
  - Core: 641, Ast: 73, Parser: 78, Storage: 76, Debugger: 23, Clr: 29,
    Compiler: 9, Modules: 9, BuiltIns: 16, Runtime: 8.

---

### Roadmap Documentation Update (2026-03-20)

**Date:** 2026-03-20

**Contributor:** @copilot

**What was done:**

1. **Added Milestone 5** to Section 5 (Timeline and Milestones) — covers
   Phase 9 (Runtime Extraction) and Phase 10 (Cleanup and Final Migration)
   with task checklists and target metrics.

2. **Updated Risk Mitigation table** (Section 7) — added Status column to all
   risk entries; added 3 new risks: module initializer ordering, bridge
   accumulation, and external consumer breakage (`WebAtoms.XF`).

3. **Updated Success Criteria** (Section 8) — added criteria #7 (no circular
   dependencies) and #8 (downstream build instructions verified); added
   "Current Progress Against Success Criteria" table tracking status of all
   8 criteria.

4. **Added Phase Progress Summary table** to Section 9 — shows status, date,
   and blockers for all 10 phases at a glance.

5. **Added Contributor & Reviewer Checklist** (Section 13) — pre-merge
   checklist for contributors (10 items), review checklist for reviewers
   (7 items), and per-assembly verification matrix showing current status
   of tests, circular deps, `InternalsVisibleTo` tracking, and downstream docs.

6. **Added Phase 10 planning section** (Section 14) — details the cleanup and
   final migration work: Clr bridge resolution (34 sites), Compiler bridge
   resolution (~6 sites), legacy entry removal, meta-package creation,
   downstream consumer updates, and coverage integration.

7. **Updated Remaining Work section** — added Resolution column to Clr and
   Compiler audit tables with specific resolution steps; restructured
   phase completion checklist with dependency annotations; added coverage
   integration to infrastructure items.

8. **Added Phase 9 Task Checklist** — detailed actionable checklists for
   Phase 9a (KeyString + property types, 10 tasks) and Phase 9b (value type
   system, 12 tasks).

---

### Phase 10 — InternalsVisibleTo Elimination and Downstream Updates (2026-03-20)

**Status:** ✅ Complete

**Date:** 2026-03-20

**Contributor:** @copilot

**What was done:**

1. **Eliminated `InternalsVisibleTo("Broiler.JavaScript.Clr")` bridge** — resolved
   all 30 compilation errors that occur when the bridge is removed:
   - Added `JSString.StringValue` public read-only property (exposes internal
     `value` field). Updated `ClrProxy.cs` to use `.StringValue`.
   - Added `JSNumber.NumberValue` public read-only property (exposes internal
     `value` field). Updated `ClrProxy.cs` to use `.NumberValue`.
   - Updated `ClrModule.cs` to use existing `JSDate.Value` public property
     instead of internal `value` field.
   - Made `NumberParser.CoerceToNumber` public (was `internal static`).
   - Made `TypeExtensions` class public (was `internal static`), including
     `GetElementTypeOrGeneric` and all reflection helper methods.
   - Made `ExpressionHelper` class public (was `internal static`), containing
     the `ToJSValue` extension method used by `JSFieldInfo.cs`.
   - Made `JSFunction(JSFunctionDelegate, JSFunction)` constructor public (was
     `internal`). Changed from `protected` to `public` because `ClrType.cs`
     uses `new JSFunction(delegate, this)` which requires public access.

2. **Eliminated `InternalsVisibleTo("Broiler.JavaScript.Compiler")` bridge** —
   resolved all 44 compilation errors:
   - Made `ExpressionHelper` class public — 22 `ToJSValue` errors resolved.
   - Made `NewLambdaExpression` class public — 8 `CallExpression` errors resolved.
   - Made `ListOfExpressionsExtensions` class public and all `ConvertTo*`
     methods public (was `internal static`) — 8 errors resolved.
   - Made `JSVariable.ValueExpression` public (was `internal static`) — 6
     errors resolved.

3. **Removed legacy `InternalsVisibleTo("Broiler.JavaScript.Tests")` entry** —
   the `Broiler.JavaScript.Tests` project does not exist in the repository;
   it was a leftover from the original YantraJS project.

4. **Updated downstream consumers to explicit satellite assembly references:**
   - `src/Broiler.Cli/Broiler.Cli.csproj` — added explicit `<ProjectReference>`
     entries for Clr, Compiler, Modules, BuiltIns, and Debugger assemblies.
   - `src/Broiler.App/Broiler.App.csproj` — added explicit `<ProjectReference>`
     entries for Clr, Compiler, Modules, BuiltIns, and Debugger assemblies.
   - This ensures satellite assemblies are included in the output and their
     module initializers run at startup.

5. **Phase 9 feasibility analysis performed:**
   - Analyzed `KeyString`/`KeyStrings` dependencies for Phase 9a
     (move to Ast). Found blocking circular dependencies:
     `PropertyKey` → `JSSymbol` (Core type), `KeyString.ToJSValue()` → `JSString`
     (Core type), `KeyStrings` → `ConcurrentStringMap` (Storage).
   - Recommendation: move `KeyString`/`KeyStrings` to Runtime (not Ast) as part
     of Phase 9b, after `JSValue`/`JSSymbol`/`JSString` are in Runtime.
   - Contract interfaces (`IBuiltInRegistry`, `IClrInterop`, `IDebugger`,
     `IJSCompiler`) blocked from moving to Runtime until Phase 9b completes.

6. **Documentation updated:**
   - Milestone 5 — updated Phase 10 checklist (all items except meta-package
     creation marked complete).
   - InternalsVisibleTo Status table (Section 6.3) — fully rewritten to reflect
     current state: only `Core.Tests`, `Runtime`, and `WebAtoms.XF` entries
     remain.
   - Phase Progress Summary — updated Phase 5 (Clr), Phase 7 (Compiler), and
     Phase 10 (Cleanup) status.
   - Per-Assembly Verification Matrix — Clr and Compiler marked as "✅ Removed".
   - Remaining Work — updated with resolution details for Clr and Compiler.
   - Section 14 — added Phase 9 feasibility analysis.
   - Success Criteria — updated criteria #4, #5, #8 to reflect completion.

**Final `InternalsVisibleTo` status (Core `AssemblyInfo.cs`):**

```csharp
[assembly: InternalsVisibleTo("Broiler.JavaScript.Core.Tests")]  // test access
[assembly: InternalsVisibleTo("Broiler.JavaScript.Runtime")]      // dynamic assembly
[assembly: InternalsVisibleTo("WebAtoms.XF")]                     // external consumer
```

**Verification:**
- All 10 production assemblies compile with zero errors.
- All **962** tests pass across 10 test projects:
  - Core: 641, Ast: 73, Parser: 78, Storage: 76, Debugger: 23, Clr: 29,
    Compiler: 9, Modules: 9, BuiltIns: 16, Runtime: 8.

---

## 13. Contributor & Reviewer Checklist

Use this checklist when contributing to or reviewing any extraction phase PR.

### Pre-Merge Checklist (Contributors)

- [ ] **No circular dependencies.** Run `dotnet list reference` on the modified
  assemblies and confirm the dependency graph is unidirectional (see Section 2.2).
- [ ] **Assembly-specific test project exists** with ≥ 90% line coverage on the
  extracted assembly. Use `dotnet test --collect:"XPlat Code Coverage"` with
  `coverlet` to measure coverage.
- [ ] **All existing tests pass.** Run the full test suite
  (`dotnet test Broiler.JavaScript/Broiler.JavaScript.*.Tests/`) and confirm
  all **974+** tests pass across 10 projects.
- [ ] **CI passes on all platforms.** Verify the GitHub Actions CI workflow
  (`.github/workflows/ci.yml`) succeeds on Ubuntu, Windows, and macOS.
- [ ] **Downstream build instructions are updated.** If the change adds a new
  assembly or moves a public type, update Section 11 (Downstream Consumer
  Migration Guide) with the new reference requirements.
- [ ] **`InternalsVisibleTo` bridges are tracked.** If any new `InternalsVisibleTo`
  entries are added, document them in Section 6.3 with the specific internal
  members accessed and a resolution plan.
- [ ] **`TypeForwardedTo` attributes are added** for any type moved to a new
  assembly. This preserves binary compatibility for downstream consumers.
- [ ] **`global using` aliases are added** in the source assembly for any
  namespace change. This preserves source-level backward compatibility.
- [ ] **Module initializer registered** (if applicable). Satellite assemblies
  must register their services in a `[ModuleInitializer]` method. Test project
  bootstraps must force-load the assembly via
  `RuntimeHelpers.RunModuleConstructor`.
- [ ] **No functional changes.** Extraction PRs must be structural moves only —
  no logic changes, no new features, no bug fixes mixed in.

### Review Checklist (Reviewers)

- [ ] **Interface stability.** Verify that cross-assembly interfaces (`IClrInterop`,
  `IBuiltInRegistry`, `IDebugger`, `IJSCompiler`, `IJSModuleResolver`) have not
  changed signature. Any signature change requires a migration note.
- [ ] **Test suite coverage.** Confirm the extracted assembly's test project
  exists and has meaningful tests covering the public API surface. Check that
  tests reference only the target assembly (plus test helpers) — no unnecessary
  Core dependency.
- [ ] **Consumer update instructions.** If the PR moves public types, verify that
  Section 11 is updated with explicit references required by downstream projects.
- [ ] **`InternalsVisibleTo` direction.** New `InternalsVisibleTo` entries must
  only go from lower-level assemblies to higher-level ones (e.g., Core →
  Compiler is OK; Compiler → Core is not). Each entry must have a documented
  resolution plan.
- [ ] **Dependency graph.** Verify no new assembly references create a circular
  dependency. The dependency graph must remain a DAG.
- [ ] **Namespace conventions.** Confirm moved types use the namespace matching
  the target assembly name (Section 6.2). `global using` aliases bridge old
  namespaces.
- [ ] **Build artifact check.** Confirm no unintended files (build outputs,
  test results, IDE settings) are included in the PR.

### Per-Assembly Verification Matrix

| Assembly | Tests Exist | Tests Pass | No Circular Deps | `InternalsVisibleTo` Tracked | Downstream Docs Updated |
|----------|:-----------:|:----------:|:----------------:|:---------------------------:|:----------------------:|
| Ast | ✅ 73 | ✅ | ✅ | N/A | ✅ |
| Parser | ✅ 78 | ✅ | ✅ | N/A | ✅ |
| Storage | ✅ 100 | ✅ | ✅ | N/A | ✅ |
| Debugger | ✅ 23 | ✅ | ✅ | ✅ Removed | ✅ |
| Clr | ✅ 29 | ✅ | ✅ | ✅ Removed | ✅ |
| BuiltIns | ✅ 29 | ✅ | ✅ | N/A | ✅ |
| Compiler | ✅ 9 | ✅ | ✅ | ✅ Removed | ✅ |
| Modules | ✅ 9 | ✅ | ✅ | N/A | ✅ |
| Runtime | ✅ 20 | ✅ | ✅ | ✅ Dynamic assembly (required) | ✅ |
| ModuleExtensions | ✅ 13 | ✅ | ✅ | N/A | ✅ |

---

## 14. Phase 10 — Cleanup and Final Migration

### Overview

Phase 10 covers the final cleanup steps after Phase 9 (Runtime Extraction)
completes. This phase eliminates all remaining `InternalsVisibleTo` migration
bridges, creates the `Broiler.JavaScript.All` meta-package, and updates all
downstream consumers to use explicit assembly references.

### Tasks

1. **Resolve remaining Clr `InternalsVisibleTo` accesses:** ✅ **Complete**
   (2026-03-20)
   - ✅ Added `JSString.StringValue` public read-only property.
   - ✅ Added `JSNumber.NumberValue` public read-only property.
   - ✅ `JSDate` — Clr code updated to use existing public `Value` property.
   - ✅ Made `NumberParser.CoerceToNumber` public.
   - ✅ Made `TypeExtensions` class and all methods public (includes
     `GetElementTypeOrGeneric`).
   - ✅ Made `ExpressionHelper` class public (contains `ToJSValue` extension).
   - ✅ Made `JSFunction(JSFunctionDelegate, JSFunction)` constructor public.
   - ✅ Removed `InternalsVisibleTo("Broiler.JavaScript.Clr")` entry.

2. **Resolve remaining Compiler `InternalsVisibleTo` accesses:** ✅ **Complete**
   (2026-03-20)
   - ✅ Made `ExpressionHelper` class public (22 `ToJSValue` errors resolved).
   - ✅ Made `NewLambdaExpression` class public (8 `CallExpression` errors).
   - ✅ Made `ListOfExpressionsExtensions` class and all `ConvertTo*` methods
     public (8 errors resolved).
   - ✅ Made `JSVariable.ValueExpression` public (6 errors resolved).
   - ✅ Removed `InternalsVisibleTo("Broiler.JavaScript.Compiler")` entry.

3. **Remove legacy `InternalsVisibleTo` entries:** ✅ **Partially complete**
   - ✅ Removed `InternalsVisibleTo("Broiler.JavaScript.Tests")` — project does
     not exist in repository.
   - ⏳ Coordinate with `WebAtoms.XF` maintainers on migration timeline.

4. **Create `Broiler.JavaScript.All` meta-package:** ✅ **Complete** (2026-03-20)
   - ✅ Created `Broiler.JavaScript.All` project at
     `Broiler.JavaScript/Broiler.JavaScript.All/Broiler.JavaScript.All.csproj`.
   - ✅ References all engine assemblies: Core, Clr, Compiler, Modules,
     BuiltIns, Debugger.
   - ✅ Downstream consumers (`Broiler.Cli`, `Broiler.App`) updated to use
     single `Broiler.JavaScript.All` reference.

5. **Update downstream consumers:** ✅ **Complete** (2026-03-20)
   - ✅ `Broiler.Cli`: updated to single `Broiler.JavaScript.All` reference
     (replaces Core + 5 explicit satellite references).
   - ✅ `Broiler.App`: updated to single `Broiler.JavaScript.All` reference
     (replaces Core + 5 explicit satellite references).

6. **Integrate coverage measurement into CI:** ✅ **Complete** (2026-03-20)
   - ✅ `coverlet.collector` v6.0.2 added to all 10 test projects.
   - ✅ CI configured to collect `XPlat Code Coverage` on each test step.
   - ⏳ Coverage report aggregation and ≥ 90% enforcement gate to be added
     when coverage baselines are established.

### Phase 9 — KeyString/KeyStrings Feasibility Analysis

**Date:** 2026-03-20

A feasibility analysis was performed for Phase 9a (moving `KeyString`/`KeyStrings`
to the Ast assembly). The move is **currently blocked** due to:

1. **`PropertyKey` contains `JSSymbol`** — The `PropertyKey` struct (defined in
   `KeyString.cs`) holds a `JSSymbol` field. `JSSymbol` is a Core type, so
   moving `PropertyKey` to Ast would require Ast to reference Core, creating
   a circular dependency.

2. **`KeyString.ToJSValue()` creates `JSString`** — The `ToJSValue()` method
   returns a `JSValue` and constructs a `JSString` instance. Both types are
   in Core.

3. **`KeyStrings` uses Core storage types** — `KeyStrings` depends on
   `ConcurrentStringMap<T>` and `ConcurrentUInt32Map<T>` from the Storage
   assembly. It also has a `GetJSString()` method that returns `JSString`.

4. **84 files reference `KeyString`** across 7+ assemblies. Moving would require
   widespread reference reorganization.

**Resolution path:** `KeyString`/`KeyStrings` should move to Runtime (not Ast)
as part of Phase 9b, after `JSValue`, `JSSymbol`, and `JSString` are already in
Runtime. The `PropertyKey` struct should either:
- Move with `KeyString` to Runtime (since it depends on `JSSymbol`), or
- Be split: `KeyType` enum and core `KeyString` struct move to Ast/Primitives,
  while `PropertyKey` stays in Runtime.

---

## 15. Meta-Package and Solution Consolidation (2026-03-20)

### Overview

Created the `Broiler.JavaScript.All` meta-package and consolidated the solution
file to include all extracted assemblies and their test projects.

### Changes

1. **Created `Broiler.JavaScript.All` meta-package:**
   - New project at `Broiler.JavaScript/Broiler.JavaScript.All/Broiler.JavaScript.All.csproj`.
   - Transitively includes: Core, Clr, Compiler, Modules, BuiltIns, Debugger.
   - Consumers reference one project instead of six individual assemblies.

2. **Updated downstream consumers to use meta-package:**
   - `Broiler.Cli`: replaced Core + 5 explicit satellite references with single
     `Broiler.JavaScript.All` reference.
   - `Broiler.App`: replaced Core + 5 explicit satellite references with single
     `Broiler.JavaScript.All` reference.

3. **Added missing projects to solution file (`YantraJS.sln`):**
   - `Broiler.JavaScript.Clr` + `Broiler.JavaScript.Clr.Tests`
   - `Broiler.JavaScript.Compiler.Tests` (Compiler was already present)
   - `Broiler.JavaScript.Modules` + `Broiler.JavaScript.Modules.Tests`
   - `Broiler.JavaScript.BuiltIns` + `Broiler.JavaScript.BuiltIns.Tests`
   - `Broiler.JavaScript.Runtime.Tests`
   - `Broiler.JavaScript.All`

### Verification

All 969 tests pass across 10 test projects:

| Assembly | Tests | Status |
|----------|-------|--------|
| Core | 641 | ✅ Pass |
| Ast | 73 | ✅ Pass |
| Parser | 78 | ✅ Pass |
| Storage | 76 | ✅ Pass |
| Debugger | 23 | ✅ Pass |
| Clr | 29 | ✅ Pass |
| Compiler | 9 | ✅ Pass |
| Modules | 9 | ✅ Pass |
| BuiltIns | 16 | ✅ Pass |
| Runtime | 15 | ✅ Pass |
| **Total** | **969** | ✅ |

### InternalsVisibleTo Validation

Confirmed that Core's `AssemblyInfo.cs` contains only three
`InternalsVisibleTo` entries — all expected, no migration bridges remain:

| Entry | Purpose | Status |
|-------|---------|--------|
| `Broiler.JavaScript.Core.Tests` | Test project access | ✅ Expected |
| `Broiler.JavaScript.Runtime` | Dynamic assembly generation | ✅ Required |
| `WebAtoms.XF` | External consumer | ⏳ Cannot remove unilaterally |

### Remaining Work

- [x] Move `IJSModuleResolver` to Runtime ✅ (2026-03-20)
- [x] Move `ExportAttribute`/`DefaultExportAttribute` to Runtime ✅ (2026-03-20)
- [x] Move `CancellableDisposableAction` to Runtime ✅ (2026-03-20)
- [x] Integrate `coverlet` coverage measurement into CI ✅ (2026-03-20)
- [x] Create `.github/workflows/ci.yml` CI workflow ✅ (2026-03-20)
- [ ] Phase 9a — Move `KeyString`/`KeyStrings` to Runtime (blocked by JSSymbol/
  JSValue/JSString dependencies; deferred until Phase 9b)
- [x] Phase 9a — Move `JSProperty`, `PropertySequence`, `ElementArray` to Storage
  with interface-typed fields/params ✅ (2026-03-21)
- [ ] Phase 9b — Move `JSValue`, `JSObject`, `JSFunction`, `JSContext`,
  `Arguments`, `CoreScript`, `Bootstrap` to Runtime (high effort, 500+ files)
- [ ] Move contract interfaces (`IBuiltInRegistry`, `IClrInterop`, `IDebugger`,
  `IJSCompiler`) to Runtime (blocked by Phase 9b)
- [ ] Phase 6 continued — Extract additional built-in types to BuiltIns
  (blocked by deep structural coupling)
- [ ] Remove temporary `InternalsVisibleTo` bridges after all phases complete
- [ ] Update downstream consumers to use new satellite references instead of
  monolithic Core references (after Phase 9b)
- [ ] Add coverage report aggregation and ≥ 90% enforcement gate to CI

---

### Continued Implementation Progress (2026-03-20, Runtime Contract Migration)

**Date:** 2026-03-20

**Contributor:** @copilot

**What was done:**

1. **Moved `ExportAttribute` and `DefaultExportAttribute` to Runtime assembly:**
   - Created `Broiler.JavaScript.Runtime/ExportAttribute.cs` and
     `Broiler.JavaScript.Runtime/DefaultExportAttribute.cs`.
   - Preserved namespace `Broiler.JavaScript.Core.Core.Module` for backward
     compatibility.
   - Added `[assembly: TypeForwardedTo(typeof(ExportAttribute))]` and
     `[assembly: TypeForwardedTo(typeof(DefaultExportAttribute))]` to Core's
     `AssemblyInfo.cs`.
   - Core source files replaced with comments pointing to Runtime.
   - These attribute types have zero runtime dependencies — they extend
     `System.Attribute` only — making them ideal candidates for early Runtime
     migration.

2. **Created `.github/workflows/ci.yml`:**
   - Multi-platform matrix: Ubuntu, Windows, macOS.
   - Full checkout depth for Nerdbank.GitVersioning compatibility.
   - Build all projects via `YantraJS.sln`.
   - Run all 10 test projects with `--collect:"XPlat Code Coverage"`.
   - Note: The roadmap previously documented CI as existing, but the workflow
     file was not present in the repository. This is now corrected.

3. **Added `coverlet.collector` v6.0.2 to all 10 test projects:**
   - Core.Tests, Ast.Tests, Parser.Tests, Storage.Tests, Debugger.Tests,
     Clr.Tests, Compiler.Tests, Modules.Tests, BuiltIns.Tests, Runtime.Tests.
   - Coverage data collected in Cobertura XML format by CI.

4. **Added 7 new Runtime tests** for `ExportAttribute`/`DefaultExportAttribute`:
   - Default name (null), custom name, attribute inheritance, assembly location.
   - Runtime.Tests now has 15 tests (was 8).

5. **Phase 9 type movability analysis performed:**
   - Analyzed all types in `Broiler.JavaScript.Core/Core/` for movability to
     Runtime (which depends only on Ast and Storage).
   - **Movable types (3):** `ExportAttribute` ✅ moved, `DefaultExportAttribute`
     ✅ moved, `CancellableDisposableAction` ✅ moved.
   - **Blocked types (9+):** `ICodeCache`, `DictionaryCodeCache`,
     `JSFunctionDelegate`, `JSClosureFunctionDelegate`, `UniqueID`,
     `CallStackItem`, `JSConstants`, `JSException`, `PrototypeAttribute` — all
     reference `JSValue`, `JSContext`, `JSFunction`, or other Core types.
   - **Key insight:** The only types movable to Runtime before Phase 9b
     (JSValue/JSContext migration) are pure data/attribute types with no
     runtime dependencies. The vast majority of Core types form a tightly
     coupled graph anchored by `JSValue` and `JSContext`.

6. **Roadmap document comprehensively updated:**
   - Project Inventory table: Runtime status updated; Runtime.Tests count
     updated to 15.
   - Milestone 5: Phase 9 progress noted (contracts being migrated); Phase 10
     marked complete; CI and coverlet integration marked done.
   - Phase Progress Summary: Phase 9 updated to "In progress"; Phase 10
     updated to "Complete".
   - Success Criteria: Coverage and CI metrics updated.
   - Testing Requirements: Runtime tests updated to 15.
   - Test Matrix: Runtime updated to 15; total to 969.
   - Contracts table: ExportAttribute/DefaultExportAttribute added as moved.
   - CI/CD section: Coverage configuration documented; local usage instructions
     added.
   - Risk Mitigation: Bridge accumulation risk updated to "Complete".
   - Phase 14 (Cleanup): Coverage integration marked complete.
   - Remaining Work: Restructured with completed items and detailed blockers.

**Verification:**
- All 10 production assemblies compile with zero errors.
- All **969** tests pass across 10 test projects:
  - Core: 641, Ast: 73, Parser: 78, Storage: 76, Debugger: 23, Clr: 29,
    Compiler: 9, Modules: 9, BuiltIns: 16, Runtime: 15.

---

## 16. Continuation Issue — Phase 9 Implementation Tracking

### Tracking Issue

**Issue:** *Continue JavaScript Engine Assembly Refactor: Implementation and Roadmap Update*
(See linked issues in this repository for the tracking issue.)

This section tracks the ongoing work for Phase 9 (Runtime Extraction) and
remaining refactor milestones, coordinating the next steps after Phases 1–8 and
Phase 10 (cleanup) have been completed.

### Current Runtime Assembly Contents

| Type | Namespace | Moved From | Date |
|------|-----------|-----------|------|
| `IJSModuleResolver` | `Broiler.JavaScript.Core.Core.Module` | Core | 2026-03-20 |
| `ExportAttribute` | `Broiler.JavaScript.Core.Core.Module` | Core | 2026-03-20 |
| `DefaultExportAttribute` | `Broiler.JavaScript.Core.Core.Module` | Core | 2026-03-20 |
| `CancellableDisposableAction` | `Broiler.JavaScript.Core.Core` | Core | 2026-03-20 |
| `ObjectStatus` | `Broiler.JavaScript.Core.Core.Object` | Core | 2026-03-21 |
| `StringExtensions` | `Broiler.JavaScript.Core.Extensions` | Core | 2026-03-21 |
| `JSValue` | `Broiler.JavaScript.Core.Core` | Core | 2026-03-21 |
| `Arguments` | `Broiler.JavaScript.Core.Core` | Core | 2026-03-21 |
| `PropertyKey` | `Broiler.JavaScript.Core.Core` | Core | 2026-03-21 |
| `JSFunctionDelegate` | `Broiler.JavaScript.Core.Core` | Core | 2026-03-21 |
| `IElementEnumerator` | `Broiler.JavaScript.Core.Core` | Core | 2026-03-21 |
| `IJSPrototype` | `Broiler.JavaScript.Core.Core` | *New* (interface) | 2026-03-21 |
| `IJSSymbol` | `Broiler.JavaScript.Core.Core` | *New* (interface) | 2026-03-21 |
| `IDebugger` | `Broiler.JavaScript.Core.Debugger` | Core | 2026-03-21 |
| `IClrInterop` | `Broiler.JavaScript.Core.Core.Clr` | Core | 2026-03-21 |
| `IJSCompiler` | `Broiler.JavaScript.Core.FastParser.Compiler` | Core | 2026-03-21 |
| `ICodeCache` | `Broiler.JavaScript.Core.Emit` | Core | 2026-03-21 |
| `JSCode` | `Broiler.JavaScript.Core.Emit` | Core | 2026-03-21 |
| `JSCodeCompiler` | `Broiler.JavaScript.Core.Emit` | Core | 2026-03-21 |

### Current Storage Assembly Contents (Types Moved from Core)

| Type | Namespace | Moved From | Date |
|------|-----------|-----------|------|
| `KeyType` | `Broiler.JavaScript.Core.Core` | Core | 2026-03-20 |
| `KeyString` | `Broiler.JavaScript.Core.Core` | Core | 2026-03-20 |
| `KeyStrings` | `Broiler.JavaScript.Core.Core` | Core | 2026-03-20 |
| `JSProperty` | `Broiler.JavaScript.Core.Core.Storage` | Core | 2026-03-20 |
| `JSObjectProperty` | `Broiler.JavaScript.Core.Core.Storage` | Core | 2026-03-21 |
| `PropertySequence` | `Broiler.JavaScript.Core.Core.Storage` | Core | 2026-03-21 |
| `ElementArray` | `Broiler.JavaScript.Core.Core.Storage` | Core | 2026-03-21 |
| `Updater<TKey,TValue>` | `Broiler.JavaScript.Core.Core.Storage` | Core | 2026-03-21 |

All moved types have `TypeForwardedTo` attributes in Core for binary
compatibility. Original namespaces are preserved for backward compatibility.

### Phase 9 Blockers — Detailed Dependency Analysis

#### Phase 9b Status: ✅ Complete (Core Value Types Moved)

Phase 9b has been completed. `JSValue`, `Arguments`, `PropertyKey`,
`JSFunctionDelegate`, and `IElementEnumerator` have been moved to the Runtime
assembly. New interface abstractions (`IJSPrototype`, `IJSSymbol`) were created
to decouple the moved types from concrete Core classes.

The remaining extraction work (Phase 9c: `JSObject`, `JSFunction`, `JSContext`)
is still blocked by extensive cross-assembly dependencies (~500+ files).

| Item | Depends On | Status |
|------|-----------|--------|
| ~~`KeyString`/`KeyStrings`~~ → Storage | ~~`JSSymbol`, `JSString`, `JSValue`~~ | ✅ 9a |
| ~~`JSProperty`~~ → Storage | ~~`JSValue`, `JSFunction`~~ → `IPropertyValue`/`IPropertyAccessor` | ✅ 9a |
| ~~`PropertySequence`/`ElementArray`~~ → Storage | ~~`JSValue`, `JSFunction`, `JSContext`~~ → `IPropertyValue`/`IPropertyAccessor` | ✅ 9a |
| ~~`JSValue`/`Arguments`/`PropertyKey`~~ → Runtime | Interface abstractions + factory delegates | ✅ 9b |
| ~~`JSFunctionDelegate`/`IElementEnumerator`~~ → Runtime | `JSValue`, `Arguments` (moved simultaneously) | ✅ 9b |
| `IBuiltInRegistry` → Runtime | `JSContext` parameter type (still in Core) | ⏳ 9c |
| ~~`IClrInterop`~~ → Runtime | `JSValue` in Runtime ✅ | ✅ 9c |
| ~~`IDebugger`~~ → Runtime | `JSValue` in Runtime ✅ | ✅ 9c |
| ~~`IJSCompiler`~~ → Runtime | `JSFunctionDelegate` in Runtime ✅; `ICodeCache` moved together | ✅ 9c |
| Additional BuiltIns → BuiltIns | Deep structural coupling to `JSArray`, `JSString`, etc. | ⏳ Phase 6 |

#### Circular Dependency: Runtime ↔ Storage — Resolved

The original circular dependency between Runtime and Storage has been resolved
through interface abstractions:

```
Runtime → Storage:  JSValue uses PropertySequence, ElementArray, KeyString
                    (via Storage project reference — one-way dependency ✅)

Storage → Runtime:  JSProperty uses IPropertyValue/IPropertyAccessor (Ast ✅).
                    PropertySequence uses IPropertyValue/IPropertyAccessor params (Ast ✅).
                    ElementArray uses IPropertyValue/IPropertyAccessor params (Ast ✅).
                    No dependency on Runtime assembly. ✅
```

#### Post-Phase 9b Extraction Status

Types now in Runtime:
- ✅ `IJSModuleResolver`, `ExportAttribute`, `DefaultExportAttribute` (contracts)
- ✅ `CancellableDisposableAction`, `ObjectStatus`, `StringExtensions` (utilities)
- ✅ `JSValue`, `Arguments`, `PropertyKey` (core value types)
- ✅ `JSFunctionDelegate`, `IElementEnumerator` (delegates/interfaces)
- ✅ `IJSPrototype`, `IJSSymbol` (new interface abstractions)

Types remaining in Core (Phase 9c candidates):
- `JSObject` (~1050 lines), `JSFunction`, `JSContext`, `JSPrototype`, `JSSymbol`
- `CoreScript`, `Bootstrap`, contract interfaces

### Remaining Refactor Milestones

| # | Milestone | Status | Key Blockers | Estimated Effort |
|---|-----------|--------|-------------|-----------------|
| 1 | Phase 9a — Move storage types to Storage | ✅ **Complete** | — | — |
| 2 | Phase 9b — Move core value types to Runtime | ✅ **Complete** | JSValue, Arguments, PropertyKey, JSFunctionDelegate, IElementEnumerator moved | — |
| 3 | Phase 9c — Move JSObject/JSFunction/JSContext to Runtime | ⏳ Not started | ~500+ file references; requires extensive factory work | **High** |
| 4 | Contract interfaces → Runtime | ⏳ Blocked by 9c | Depends on JSContext/JSObject remaining in Core | Low — 4 interfaces |
| 5 | Phase 6 — Additional BuiltIns extraction | ⏳ Partially blocked | Deep structural coupling (JSArray, JSString, etc.) | High |
| 6 | `InternalsVisibleTo` final cleanup | ⏳ | Remove remaining bridges after Phase 9c | Low |
| 7 | Coverage enforcement gate | ⏳ | Requires coverage baselines | Low |

### Integration and Cleanup Status

| Category | Status | Details |
|----------|--------|---------|
| **CI Pipeline** | ✅ Complete | `.github/workflows/ci.yml` — Ubuntu/Windows/macOS matrix; 10 test projects |
| **Coverage Collection** | ✅ Complete | `coverlet.collector` v6.0.2 in all test projects; `XPlat Code Coverage` in CI |
| **Coverage Enforcement** | ⏳ Pending | ≥ 90% line coverage gate not yet added |
| **Meta-Package** | ✅ Complete | `Broiler.JavaScript.All` created; downstream consumers updated |
| **Downstream Consumers** | ✅ Complete | `Broiler.App` and `Broiler.Cli` use `All` meta-package |
| **Migration Bridges** | ✅ Complete | All `InternalsVisibleTo` migration bridges eliminated |
| **TypeForwardedTo** | ✅ Active | 21 forwarding attributes in Core for binary compatibility |
| **Global Using** | ✅ Active | `Broiler.JavaScript.Ast`, `.Parser`, `.Storage` in Core's `GlobalUsings.cs` |
| **External Consumer** | ⏳ Pending | `WebAtoms.XF` `InternalsVisibleTo` entry retained; needs external coordination |

### Verification (2026-03-21)

All 10 production assemblies compile with zero errors.
All **998** tests pass across 10 test projects:
- Core: 641, Ast: 73, Parser: 78, Storage: 100, Debugger: 23, Clr: 29,
  Compiler: 9, Modules: 9, BuiltIns: 16, Runtime: 20.

---

## 20. Phase 9a Completion — PropertySequence and ElementArray Migration (2026-03-21)

### Overview

This iteration completes Phase 9a by moving `PropertySequence` and
`ElementArray` from Core to the Storage assembly, resolving the last
remaining Storage↔Core circular dependency for property storage types.

### Changes Made

#### 1. PropertySequence → Storage Assembly

**File moved:** `Core/Storage/PropertySequence.cs` → `Broiler.JavaScript.Storage/PropertySequence.cs`

**Method signature changes:**
| Method | Before | After |
|--------|--------|-------|
| `Put(uint, JSValue, ...)` | `JSValue` parameter | `IPropertyValue` parameter |
| `Put(KeyString, JSValue, ...)` | `JSValue` parameter | `IPropertyValue` parameter |
| `Put(KeyString, JSFunction, JSFunction, ...)` | `JSFunction` parameters | `IPropertyAccessor` parameters |
| `Put(KeyString, JSFunctionDelegate, ...)` | `JSFunctionDelegate` params | Moved to `PropertySequenceCoreExtensions` in Core |

**New public API:**
- `PropertySequence.GetMap()` — Returns ref to internal map for enumerators.
- `PropertySequence.Head` — Returns head index for enumerators.
- `PropertySequence.TypeErrorFactory` — Static delegate for error creation in
  `RemoveAt`. Initialized by `PropertySequenceCoreExtensions.[ModuleInitializer]`
  in Core to produce `JSException` (TypeError). Falls back to
  `InvalidOperationException` if not set.

**Extracted types:**
- `PropertyValueEnumerator` (formerly `PropertySequence.ValueEnumerator`) moved
  to `Core/Storage/PropertyValueEnumerator.cs` because it depends on `JSObject`
  and `JSValue` (Core runtime types). All 7 call sites updated.
- `PropertySequenceCoreExtensions` created in Core with:
  - `Put(ref this PropertySequence, ...)` extension for `JSFunctionDelegate` params.
  - `[ModuleInitializer]` that sets `PropertySequence.TypeErrorFactory`.

**Types that stayed in PropertySequence (Storage):**
- `JSObjectProperty` struct — no Core type dependencies.
- `Updater<TKey,TValue>` delegate — generic, no dependencies.
- `PropertyEnumerator` nested struct — depends only on Storage types.

#### 2. ElementArray → Storage Assembly

**File moved:** `Core/Storage/UIntMapArray.cs` → `Broiler.JavaScript.Storage/ElementArray.cs`

**Method signature changes:**
| Method | Before | After |
|--------|--------|-------|
| `Put(uint, JSFunction, JSFunction, ...)` | `JSFunction` params | `IPropertyAccessor` params |
| `Put(uint, JSValue, ...)` | `JSValue` param | `IPropertyValue` param |
| `QuickSort(Comparison<JSValue>, ...)` | `Comparison<JSValue>` | `Comparison<IPropertyValue>` (now `public`) |
| `InsertionSort(Comparison<JSValue>, ...)` | `Comparison<JSValue>` | `Comparison<IPropertyValue>` |

**Other changes:**
- `JSMath.RandomNumber()` replaced with `Random.Shared.NextDouble()` (standard
  .NET 6+ API, no dependency on Core's JSMath).
- `QuickSort` visibility changed from `internal` to `public` so Core callers
  (JSArrayPrototype.Sort) can access it across assembly boundary.

#### 3. Call Site Updates

- **JSArrayPrototype.Sort:** Wrapped `Comparison<JSValue>` delegate for
  `Comparison<IPropertyValue>` parameter:
  `elements.QuickSort((a, b) => cx((JSValue)a, (JSValue)b), 0, len - 1);`
- **PropertyValueEnumerator usage (7 sites):** All `PropertySequence.ValueEnumerator`
  references renamed to `PropertyValueEnumerator` in Core, Clr, and Network
  assemblies.

#### 4. TypeForwardedTo Attributes

Added to Core's `AssemblyInfo.cs`:
- `TypeForwardedTo(typeof(JSObjectProperty))`
- `TypeForwardedTo(typeof(PropertySequence))`
- `TypeForwardedTo(typeof(ElementArray))`
- `TypeForwardedTo(typeof(Updater<,>))`

Total forwarding attributes: **47** (was 43).

### Architecture Impact

The Storage↔Core circular dependency for property storage is now **fully
resolved**:

```
Before:  Storage ← PropertySequence (uses JSValue, JSFunction, JSContext)
         Storage ← ElementArray (uses JSValue, JSFunction, JSMath)

After:   Storage contains PropertySequence (uses IPropertyValue, IPropertyAccessor)
         Storage contains ElementArray (uses IPropertyValue, IPropertyAccessor)
         Core contains PropertyValueEnumerator (uses JSObject, JSValue)
         Core contains PropertySequenceCoreExtensions (uses JSFunctionDelegate)
```

All storage types now live in the Storage assembly with zero Core dependencies.
Core-dependent operations (value enumeration, delegate-based property creation,
TypeError generation) are handled by extension methods and extracted types in
Core.

### Phase 9a — Complete

Phase 9a is now **fully complete**. All planned type moves have been executed:

| Type | Target | Status |
|------|--------|--------|
| `KeyType` | Storage | ✅ |
| `KeyString` | Storage | ✅ |
| `KeyStrings` | Storage | ✅ |
| `JSProperty` | Storage (interface-typed fields) | ✅ |
| `PropertySequence` | Storage (interface-typed params) | ✅ |
| `ElementArray` | Storage (interface-typed params) | ✅ |
| `JSObjectProperty` | Storage | ✅ |
| `Updater<TKey,TValue>` | Storage | ✅ |

### Remaining Work (Phase 9b+)

Phase 9b (JSValue/JSContext → Runtime) remains the critical path. It is the
only blocker for:
- Moving `KeyString.ToJSValue()` from Core extension to Storage (depends on JSString)
- Moving contract interfaces (`IBuiltInRegistry`, `IClrInterop`, etc.) to Runtime
- Completing BuiltIns extraction (Phase 6)
- Final `InternalsVisibleTo` cleanup

### Test Results

All **998** tests pass across 10 test projects:
- Core: 641, Ast: 73, Parser: 78, Storage: 100, Debugger: 23, Clr: 29,
  Compiler: 9, Modules: 9, BuiltIns: 16, Runtime: 20.

---

## 17. Phase 9a Continued — KeyString Migration and API Surface Expansion (2026-03-20)

### Overview

This iteration completes the Phase 9a KeyString/KeyStrings migration and
expands the public API surface to unblock future BuiltIns extraction.

### KeyString/KeyStrings → Storage Assembly

**Decision:** Move KeyString, KeyStrings, and KeyType to the **Storage**
assembly rather than Ast (the original roadmap target). Rationale:

1. KeyStrings depends on `ConcurrentStringMap<T>` and `ConcurrentUInt32Map<T>`,
   both defined in Storage. Moving to Ast would create a circular dependency
   (Ast → Storage → Ast).
2. KeyString is fundamentally a storage-level interning concept — its uint key
   maps directly into property storage maps.
3. Storage already depends on Ast, and Core depends on Storage, so all existing
   consumers can see the types without additional project references.

**Types Moved:**
| Type | From | To | Binary Compat |
|------|------|----|---------------|
| `KeyType` enum | Core | Storage | `TypeForwardedTo` in Core |
| `KeyString` struct | Core | Storage | `TypeForwardedTo` in Core |
| `KeyStrings` static class | Core | Storage | `TypeForwardedTo` in Core |

**Methods Retained in Core:**
- `KeyString.ToJSValue()` — Now an extension method in `KeyStringCoreExtensions`
  (depends on `JSString`, a Core type).
- `KeyStringCoreExtensions.GetJSString(uint id)` — Replacement for the former
  `KeyStrings.GetJSString()` internal method (depends on `JSString`).

**Methods Made Public in Storage:**
- `KeyStrings.TryGet(StringSpan, out KeyString)` — Was internal, now public
  (needed by Core callers like JSObject, JSString, JSNumber).
- `KeyStrings.GetName(uint)` — Was internal, now public (needed by Core callers
  like PropertySequence).

**PropertyKey** remains in Core because it references `JSSymbol` (a Core type
with runtime dependencies).

### JSObject Public API Expansion

Made the following `internal` status-checking methods `public` on `JSObject`:

| Method | Purpose |
|--------|---------|
| `IsExtensible()` | Checks if object accepts new properties |
| `IsSealed()` | Checks if object is sealed |
| `IsFrozen()` | Checks if object is frozen |
| `IsSealedOrFrozen()` | Combined check |
| `IsSealedOrFrozenOrNonExtensible()` | Combined check |

This unblocks future BuiltIns extraction for types like `JSReflect` that call
these methods.

### JSProperty Public API Expansion

Made the following `internal static` factory methods `public` on `JSProperty`:

- `Property(JSValue, JSPropertyAttributes)` — Create value property
- `Property(uint, JSValue, JSPropertyAttributes)` — Create indexed property
- `Property(KeyString, JSValue, JSPropertyAttributes)` — Create named property
- `Property(KeyString, JSFunction, JSPropertyAttributes)` — Create function property
- `Property(KeyString, JSFunctionDelegate, JSFunctionDelegate, JSPropertyAttributes)` — Create accessor
- `Property(KeyString, JSFunction, JSFunction, JSPropertyAttributes)` — Create accessor
- `Property(JSFunction, JSFunction, JSPropertyAttributes)` — Create anonymous accessor
- `Function(KeyString, JSFunctionDelegate, JSPropertyAttributes, int)` — Create function
- `ToNotReadOnly()` — Create mutable copy

### Interface Implementation Status

Verified that interface contracts are already implemented:
- ✅ `JSValue : IPropertyValue` (already implemented)
- ✅ `JSFunction : IPropertyAccessor` (already implemented)
- ✅ Interfaces defined in `Broiler.JavaScript.Ast/IPropertyContracts.cs`

### Test Coverage

24 new tests added to `Broiler.JavaScript.Storage.Tests/KeyStringTests.cs`:
- KeyType enum values
- KeyString creation, equality, hashing, ToString, Value
- KeyStrings well-known keys, GetOrCreate, GetNameString, TryGet, GetName
- Assembly location verification (all types in Storage assembly)

**Total tests: 998** across 10 test projects:
- Core: 641, Ast: 73, Parser: 78, **Storage: 100 (+24)**, Debugger: 23,
  Clr: 29, Compiler: 9, Modules: 9, BuiltIns: 16, Runtime: 20.

### TypeForwardedTo Count

Updated to **47** forwarding attributes in Core AssemblyInfo.cs
(43 previous + 4 for JSObjectProperty/PropertySequence/ElementArray/Updater).

### Remaining Phase 9a/9b Work

| # | Milestone | Status | Notes |
|---|-----------|--------|-------|
| 1 | KeyString/KeyStrings → Storage | ✅ **Complete** | Moved with extension methods for Core-dependent operations |
| 2 | JSProperty → Storage | ✅ **Complete** | Moved with interface-typed fields (`IPropertyValue`/`IPropertyAccessor`); `JSPropertyFactory` in Core for `JSFunction`-dependent factory methods |
| 3 | PropertySequence/ElementArray → Storage | ✅ **Complete** | Moved with interface-typed params; `PropertyValueEnumerator` in Core |
| 4 | Phase 9b — Move core value types to Runtime | ⏳ Not started | 500+ file references; API breakage risk |
| 5 | Contract interfaces → Runtime | ⏳ Blocked | Depends on Phase 9b |
| 6 | JSObject status methods → public | ✅ **Complete** | Unblocks JSReflect extraction |
| 7 | JSProperty factory methods → public | ✅ **Complete** | Unblocks BuiltIns property manipulation |

---

## 18. JSProperty Migration to Storage Assembly (2026-03-20)

### Overview

This iteration completes the JSProperty migration to the Storage assembly,
resolving the circular dependency between Storage and Core for property
descriptors. The `JSProperty` struct now uses interface-typed fields
(`IPropertyValue`/`IPropertyAccessor` from the Ast assembly) instead of concrete
Core types (`JSValue`/`JSFunction`).

### Changes Made

#### 1. JSProperty → Storage Assembly

**File moved:** `Core/Storage/JSProperty.cs` → `Broiler.JavaScript.Storage/JSProperty.cs`

**Field type changes:**
| Field | Before | After |
|-------|--------|-------|
| `value` | `JSValue` (Core) | `IPropertyValue` (Ast) |
| `get` | `JSFunction` (Core) | `IPropertyAccessor` (Ast) |
| `set` | `JSFunction` (Core) | `IPropertyAccessor` (Ast) |

All constructors updated to accept interface types. Implicit upcasting means
callers constructing `JSProperty` instances with `JSValue`/`JSFunction` arguments
require no changes.

#### 2. JSPropertyFactory (New — Core)

Factory methods that create `JSFunction` objects cannot live in Storage (no Core
dependency). Extracted to `JSPropertyFactory` static class in
`Core/Storage/JSPropertyFactory.cs`:

- `JSPropertyFactory.Function(in KeyString, JSFunctionDelegate, ...)` — creates
  function properties (was `JSProperty.Function()`)
- `JSPropertyFactory.Property(in KeyString, JSFunctionDelegate, JSFunctionDelegate, ...)`
  — creates accessor properties from delegates (was `JSProperty.Property()` overload)

#### 3. Explicit Casts at Field Access Sites

~30 field access sites across 10 files updated with downcasts where the consumer
needs the concrete runtime type:

```csharp
// Before (JSProperty.value was JSValue)
return p.value;
p.set.f(new Arguments(receiver, value));

// After (JSProperty.value is IPropertyValue)
return (JSValue)p.value;
((JSFunction)p.set).f(new Arguments(receiver, value));
```

**Files modified:**
- `Core/Object/JSObject.cs` — `GetMethod()`, `SetValue()`, `GetValue()` methods
- `Core/Array/JSArray.cs` — `GetArrayElements()`, enumerator methods
- `Core/Array/JSArrayPrototype.cs` — `Pop()` method
- `Core/JSPrototype.cs` — `GetMethod()` method
- `Core/Global/JSGlobal.cs` — `StructuredCloneValue()` method
- `Core/Json/JSJSON.cs` — stringify getter/value access
- `Core/Objects/JSReflect.cs` — `Set()` method (3 overloads)
- `Core/Storage/UIntMapArray.cs` — `QuickSort()`, `InsertionSort()` methods
- `Extensions/JSPropertyExtensions.cs` — `GetValue()`, `ToJSValue()` methods
- `Debugger/V8PropertyDescriptor.cs` — property descriptor construction

#### 4. PropertySequence Update

`PropertySequence.Put(KeyString, JSFunctionDelegate, JSFunctionDelegate, ...)`
updated to use `JSPropertyFactory.Property()` instead of the removed
`JSProperty.Property()` overload.

#### 5. TypeForwardedTo

Added `TypeForwardedTo(typeof(JSProperty))` in Core's `AssemblyInfo.cs` for
binary compatibility. Total forwarding attributes: **43**.

### Architecture Impact

The circular dependency between Storage and Core for `JSProperty` is now
**fully resolved**:

```
Before:  Storage → Core (JSProperty uses JSValue, JSFunction)
After:   Storage → Ast  (JSProperty uses IPropertyValue, IPropertyAccessor)
```

This means `JSProperty` can now be referenced by any assembly that depends on
Storage, without pulling in the full Core runtime type system.

**Remaining circular dependency:** `PropertySequence` and `ElementArray` still
have method signatures that reference `JSValue`/`JSFunction`/`JSContext`. These
cannot move to Storage until Phase 9b resolves the value type system location.

### Test Results

All **998** tests pass across 10 test projects:
- Core: 641, Ast: 73, Parser: 78, Storage: 100, Debugger: 23, Clr: 29,
  Compiler: 9, Modules: 9, BuiltIns: 16, Runtime: 20.

---

## 19. Legacy Project Cleanup — Network, ModuleExtensions, NodePollyfill, CLI REPL (2026-03-21)

This iteration fixes the CI build failure caused by five legacy projects that
were not updated during the Core assembly rename from `YantraJS.Core` to
`Broiler.JavaScript.Core`. These projects had broken `<ProjectReference>` paths,
incompatible target frameworks, and stale namespace imports.

### Changes Made

#### 1. Broken Project References

Five projects referenced old `YantraJS.*` project paths that no longer exist:

| Project | Old Reference | New Reference |
|---------|--------------|---------------|
| `Broiler.JavaScript.Network` | `YantraJS.Core.csproj` | `Broiler.JavaScript.Core.csproj` |
| `Broiler.JavaScript.Network` | `YantraJS.JSClassGenerator.csproj` | `Broiler.JavaScript.JSClassGenerator.csproj` |
| `Broiler.JavaScript.ModuleExtensions` | `YantraJS.Core.csproj` | `Broiler.JavaScript.Core.csproj` |
| `Broiler.JavaScript.ModuleExtensions` | `YantraJS.ExpressionCompiler.csproj` | `Broiler.JavaScript.ExpressionCompiler.csproj` |
| `Broiler.JavaScript.NodePollyfill` | `YantraJS.Core.csproj` | `Broiler.JavaScript.Core.csproj` |
| `Broiler.JavaScript` (CLI) | `YantraJS.Core.csproj` | `Broiler.JavaScript.Core.csproj` |
| `Broiler.JavaScript` (CLI) | `YantraJS.Network.csproj` | `Broiler.JavaScript.Network.csproj` |
| `JIntPerfTests` | `YantraJS.Core.csproj` | `Broiler.JavaScript.Core.csproj` |

#### 2. Target Framework Updates

Core targets `net8.0`, so all consuming projects must target a compatible TFM:

| Project | Before | After |
|---------|--------|-------|
| `Broiler.JavaScript.Network` | `netstandard2.0;netstandard2.1` | `net8.0` |
| `Broiler.JavaScript.ModuleExtensions` | `netstandard2.0` | `net8.0` |
| `Broiler.JavaScript.NodePollyfill` | `netstandard2.0` | `net8.0` |
| `JIntPerfTests` | `net6.0` | `net8.0` |

#### 3. Namespace Migration

All `using` directives updated across 25 files in 5 projects:

| Old Namespace | New Namespace |
|---------------|---------------|
| `Yantra.Core` | `Broiler.JavaScript.Core.Core` |
| `Yantra.Core.Events` | `Broiler.JavaScript.Core.Core.Events` |
| `YantraJS.Core` | `Broiler.JavaScript.Core.Core` |
| `YantraJS.Core.Clr` | `Broiler.JavaScript.Core.Core.Clr` |
| `YantraJS.Core.Typed` | `Broiler.JavaScript.Core.Typed` |
| `YantraJS.Core.Debugger` | `Broiler.JavaScript.Core.Debugger` |
| `YantraJS.Core.FastParser` | `Broiler.JavaScript.Ast` |
| `YantraJS.Emit` | `Broiler.JavaScript.Core.Emit` |
| `YantraJS.Expressions` | `Broiler.JavaScript.ExpressionCompiler` |

Internal project namespaces (`YantraJS.Network`, `YantraJS.REPL`, etc.)
retained — they define the project's own types.

#### 4. JSClassGenerator Source Generator Fix

The source generator emitted `using Broiler.JavaScript.Core.Core.Storage;`
for `JSPropertyAttributes`, but the enum moved to the `Broiler.JavaScript.Storage`
namespace. Fixed both `ClassGenerator.cs` and `RegistrationGenerator.cs` to
emit the additional `using Broiler.JavaScript.Storage;` directive.

#### 5. API Compatibility Fixes

Several API changes in Core required code updates in legacy projects:

| Issue | Files | Fix |
|-------|-------|-----|
| `TryGetProperty` removed from `JSValue` | `FetchRequest.cs`, `Blob.cs` | Replaced with indexer + `IsNullOrUndefined` check |
| `JSContext.NewTypeError` now static | `Blob.cs`, `EventEmitter.cs` | Changed instance call to static call |
| Unassigned `CancellationToken` | `FetchApi.cs` | Initialized to `CancellationToken.None` |
| `Microsoft.Threading` unused import | 3 CLI files | Removed unused `using` directive |

#### 6. CLI REPL — Additional Project Reference

Added `Broiler.JavaScript.ExpressionCompiler` reference to the CLI REPL
project, required by `AssemblyCodeCache.cs` for `CompileToStaticMethod`.

### Architecture Impact

No architectural changes. All fixes are namespace/reference corrections to
align legacy projects with the refactored assembly structure.

### Test Results

All **998** tests pass across 10 test projects:
- Core: 641, Ast: 73, Parser: 78, Storage: 100, Debugger: 23, Clr: 29,
  Compiler: 9, Modules: 9, BuiltIns: 16, Runtime: 20.

---

---

## 20. ObjectStatus Migration and CI Fix (2026-03-21)

This iteration moves the `ObjectStatus` enum to Runtime and fixes a
cross-platform CI failure in the Runtime test suite.

### Changes Made

#### 1. `ObjectStatus` → Runtime

`ObjectStatus` is a simple flags enum (`None`, `Frozen`, `Sealed`,
`NonExtensible`, combinations) with zero type dependencies. It defines the
extensibility/freeze/seal state of a JavaScript object.

- **Moved:** `Core/Object/ObjectStatus.cs` → `Runtime/ObjectStatus.cs`
- **TypeForwardedTo:** Added
  `[assembly: TypeForwardedTo(typeof(ObjectStatus))]` in Core
  `AssemblyInfo.cs`.
- **Namespace:** Retained `Broiler.JavaScript.Core.Core.Object` for backward
  compatibility.
- **Consumers:** 4 files in Core (`JSObject.cs`, `JSObjectStatic.cs`,
  `JSReflect.cs`) — all resolved via existing Core → Runtime project reference.

#### 2. Cross-Platform Runtime Test Fix

Two tests in `IJSModuleResolverTests` failed on Windows CI:
- `Resolve_ReturnsPath_WhenModuleExists`
- `Resolve_HandlesNestedPaths`

**Root cause:** `StubModuleResolver.Resolve()` used `Path.Combine()` which
produces backslashes on Windows (e.g., `/app\math.js`), mismatching the
forward-slash keys in the module dictionary (e.g., `/app/math.js`).

**Fix:** Normalize path separators to forward slashes after `Path.Combine` in
the stub resolver. This matches JavaScript module semantics where paths always
use forward slashes.

### Architecture Impact

`ObjectStatus` joins `IJSModuleResolver`, `ExportAttribute`,
`DefaultExportAttribute`, and `CancellableDisposableAction` in the Runtime
assembly. Total TypeForwardedTo attributes in Core `AssemblyInfo.cs`: **48**.

### Test Results

All **998** tests pass across 10 test projects:
- Core: 641, Ast: 73, Parser: 78, Storage: 100, Debugger: 23, Clr: 29,
  Compiler: 9, Modules: 9, BuiltIns: 16, Runtime: 20.

### Next Immediate Steps

1. **Phase 9c (future):** Move `JSObject`, `JSFunction`, `JSContext`,
   `JSPrototype`, `JSSymbol`, `CoreScript`, `Bootstrap` to Runtime. This is the
   largest remaining task (~500+ file references to update). Requires extensive
   factory delegate work for Core-only type construction.
2. **Contract interfaces:** Once Phase 9c completes, move `IBuiltInRegistry`,
   `IClrInterop`, `IDebugger`, `IJSCompiler` to Runtime.
3. **BuiltIns extraction (Phase 6):** Continue extracting built-in objects
   (JSArray, JSString, JSNumber, etc.) — partially blocked by Phase 9c for
   assembly dependency reasons.

*This roadmap tracks the creation and documentation of the refactor plan, not
the refactor itself. Implementation issues should be created per milestone
and linked back to this document.*

---

## 21. Phase 9b Preparation — Factory Infrastructure and Analysis

### Overview

Phase 9b moved the core value type system (`JSValue`, `Arguments`,
`PropertyKey`, `JSFunctionDelegate`, `IElementEnumerator`) from Core to Runtime.
This section documents the preparatory infrastructure and the dependency
analysis that guided the move. Phase 9b is now **complete** (see Section 22).

### Preparatory Changes Already Completed

#### 1. StringExtensions → Runtime

`StringExtensions` (string comparison helpers: `Less`, `Greater`,
`LessOrEqual`, `GreaterOrEqual`, `ToCamelCase`, `IsEmpty`) moved from
`Core/Extensions/` to Runtime. These have zero Core dependencies (only
`System` types) and are used by `JSValue.Less()` / `JSValue.Greater()`
comparison methods.

- Source: `Broiler.JavaScript.Runtime/StringExtensions.cs`
- Namespace: `Broiler.JavaScript.Core.Extensions` (unchanged)
- TypeForwardedTo added in Core AssemblyInfo.cs

#### 2. ExpressionCompiler Reference Added to Runtime

Runtime now references `Broiler.JavaScript.ExpressionCompiler`, needed for
`YExpression<T>` used by `IJSCompiler.Compile()` return type.

#### 3. JSValue Factory Infrastructure

Added 12 `internal static` factory fields to `JSValue` following the proven
`PropertySequence.TypeErrorFactory` pattern:

| Field | Type | Purpose |
|-------|------|---------|
| `UndefinedValue` | `JSValue` | Singleton for `JSUndefined.Value` |
| `NullValue` | `JSValue` | Singleton for `JSNull.Value` |
| `BooleanTrue` | `JSValue` | Singleton for `JSBoolean.True` |
| `BooleanFalse` | `JSValue` | Singleton for `JSBoolean.False` |
| `NumberOne` | `JSValue` | Singleton for `JSNumber.One` |
| `NumberNaN` | `JSValue` | Singleton for `JSNumber.NaN` |
| `CreateNumber` | `Func<double, JSValue>` | Factory for `new JSNumber(v)` |
| `CreateString` | `Func<string, JSValue>` | Factory for `new JSString(v)` |
| `NewTypeError` | `Func<string, Exception>` | Factory for `JSContext.NewTypeError()` |
| `ForceConvertHelper` | `Func<JSValue, object, bool, object>` | CLR interop unwrap |
| `CreateDynamicMetaObject` | `Func<Expression, JSValue, DynamicMetaObject>` | IDynamicMetaObjectProvider |
| `NumberToECMAString` | `Func<double, string>` | `JSNumber.ToECMAString()` |

#### 4. JSValueCoreExtensions Module Initializer

Created `Core/JSValueCoreExtensions.cs` with `[ModuleInitializer]` that wires
all factory delegates at Core assembly load time, following the proven pattern
from `PropertySequenceCoreExtensions`.

### Dependency Analysis for Moving JSValue

JSValue directly references **7 Core-only types** in its method signatures:

| Type | References | Usage | Move Requirement |
|------|-----------|-------|-----------------|
| `JSObject` | 11 | Parameter/return type (virtual methods) | Must move simultaneously |
| `JSSymbol` | 10 | Parameter type (virtual methods) | Must move simultaneously |
| `JSPrototype` | 1 | Field type (`prototypeChain`) | Must move simultaneously |
| `Arguments` | 3 | Parameter type | Must move simultaneously |
| `PropertyKey` | 1 | Abstract method return type | Must move simultaneously |
| `JSFunctionDelegate` | 1 | Return type | Must move simultaneously |
| `IElementEnumerator` | 4 | Return type | Must move simultaneously |

**Total types requiring simultaneous move: 8** (JSValue + 7 dependencies).

Each dependency has its own transitive dependencies:
- `JSObject` (~1050 lines): Uses `PropertySequence`, `ElementArray`, `SAUint32Map`
  (Storage ✅ already in Runtime), plus `JSArray`, `JSBoolean`, `JSNumber`,
  `JSString` (Core — need factories)
- `JSSymbol` (~60 lines): Uses `JSContext.Current`, `JSContext.NewTypeError` (Core)
- `JSPrototype` (~200 lines): Uses `JSFunction`, `SAUint32Map` (Storage ✅),
  `Sequence` (ExpressionCompiler ✅)
- `Arguments` (~500 lines): Uses `JSArray`, `JSUndefined`, `JSSpreadValue` (Core)
- `PropertyKey` (~28 lines): Uses `JSSymbol` (must move with JSSymbol)
- `JSFunctionDelegate` (1 line): Uses `JSValue`, `Arguments` (both must be
  in Runtime)
- `IElementEnumerator`: Uses `JSValue` (must be in Runtime)

### Recommended Move Order

1. Move `JSFunctionDelegate` (1 line) + `Arguments` (~500 lines) +
   `IElementEnumerator` to the Runtime simultaneously. Arguments requires
   extensive factory work for `JSArray`/`JSUndefined` references.
2. Move `PropertyKey` + `JSSymbol` together.
3. Move `JSPrototype` (requires `JSFunction` type reference → factory).
4. Move `JSValue` (with all factory replacements).
5. Move `JSObject`, `JSFunction`, `JSContext` (largest change).
6. Move contract interfaces (`IBuiltInRegistry`, `IClrInterop`, `IDebugger`,
   `IJSCompiler`).

### Estimated Effort

- **Files to modify in Core:** ~50 (factory replacements in moved files)
- **Files unaffected:** ~450+ (namespace-qualified references resolve
  automatically via Core's project reference to Runtime)
- **New factory delegates needed:** ~30-40 (for Core-specific type
  construction and error creation in moved files)
- **TypeForwardedTo entries:** ~10 new entries in Core `AssemblyInfo.cs`

---

## 22. Phase 9b Completion — Core Value Types Moved to Runtime (2026-03-21)

### Overview

Phase 9b successfully moves the core value type system from Core to the Runtime
assembly. This includes `JSValue` (the base class for all JavaScript values),
`Arguments` (function call argument passing), `PropertyKey` (union of string and
symbol keys), `JSFunctionDelegate` (function signature delegate), and
`IElementEnumerator` (spread/iteration interface). Two new interface abstractions
(`IJSPrototype`, `IJSSymbol`) were created to decouple the moved types from
concrete Core classes that remain in Core.

### Types Moved to Runtime

| Type | Lines | Namespace | Purpose |
|------|-------|-----------|---------|
| `JSValue` | ~784 | `Broiler.JavaScript.Core.Core` | Base class for all JS values; arithmetic, comparison, property access |
| `Arguments` | ~640 | `Broiler.JavaScript.Core.Core` | Function call argument struct; spread support, rest parameters |
| `PropertyKey` | ~32 | `Broiler.JavaScript.Core.Core` | Union struct: string key (`KeyString`) or symbol key (`IJSSymbol`) |
| `JSFunctionDelegate` | ~6 | `Broiler.JavaScript.Core.Core` | Delegate type: `delegate JSValue JSFunctionDelegate(in Arguments a)` |
| `IElementEnumerator` | ~12 | `Broiler.JavaScript.Core.Core` | Interface for array/spread enumeration |

### New Interface Abstractions

Two interfaces were created in Runtime to abstract over concrete Core types:

#### `IJSPrototype` (29 lines)

Abstracts over the `JSPrototype` class (which remains in Core). Provides:
- `Object` property — the wrapped JS object
- `GetInternalProperty(KeyString)` — string-keyed property lookup
- `GetInternalProperty(uint)` — index-keyed property lookup
- `GetInternalProperty(IJSSymbol)` — symbol-keyed property lookup
- `GetMethod(KeyString)` — method delegate lookup
- `Dirty()` — marks prototype chain as stale

`JSPrototype` in Core implements this interface. `JSValue.prototypeChain`
field changed from `JSPrototype` to `IJSPrototype`.

#### `IJSSymbol` (12 lines)

Abstracts over the `JSSymbol` class (which remains in Core). Provides:
- `Key` property — the unique numeric key identifying the symbol

`JSSymbol` in Core implements this interface. `JSValue` virtual methods
accept `IJSSymbol` parameters instead of `JSSymbol`.

### Factory Delegates Added

Two new factory delegates were added to `JSValue` for Phase 9b:

| Field | Type | Purpose |
|-------|------|---------|
| `CreatePrototypeObject` | `Func<JSValue, IJSPrototype>` | Creates `JSPrototype` wrapper from a `JSValue` (wired to `(value as JSObject)?.PrototypeObject`) |
| `InvokePropertyGetter` | `Func<IPropertyAccessor, JSValue, JSValue>` | Invokes a property getter function (wired to `((JSFunction)getter).InvokeFunction(new Arguments(receiver))`) |

These join the 6 existing factory delegates (total: 8 delegates wired via
`[ModuleInitializer]` in `JSValueCoreExtensions.InitializeFactories()`).

### `JSValueCoreExtensions` Module Initializer

The `JSValueCoreExtensions.InitializeFactories()` method (Core assembly) wires
all factory delegates at Core assembly load time:

```csharp
[ModuleInitializer]
internal static void InitializeFactories()
{
    // Singleton values
    JSValue.UndefinedValue = JSUndefined.Value;
    JSValue.NullValue = JSNull.Value;
    JSValue.BooleanTrue = JSBoolean.True;
    JSValue.BooleanFalse = JSBoolean.False;
    JSValue.NumberOne = JSNumber.One;
    JSValue.NumberNaN = JSNumber.NaN;

    // Factory delegates
    JSValue.CreateNumber = v => new JSNumber(v);
    JSValue.CreateString = v => new JSString(v);
    JSValue.NewTypeError = msg => JSContext.NewTypeError(msg);
    JSValue.NumberToECMAString = JSNumber.ToECMAString;
    JSValue.CreateDynamicMetaObject = (param, value) => new JSDynamicMetaData(param, value);
    JSValue.ForceConvertHelper = ...;  // CLR interop unwrap
    JSValue.InvokePropertyGetter = (getter, receiver) =>
        ((JSFunction)getter).InvokeFunction(new Arguments(receiver));
    JSValue.CreatePrototypeObject = value => (value as JSObject)?.PrototypeObject;

    // Arguments delegates
    Arguments.Empty = new Arguments(JSUndefined.Value);
    Arguments.ForApplyImpl = ArgumentsCoreExtensions.ForApplyCore;
    Arguments.RestFromImpl = ArgumentsCoreExtensions.RestFromCore;
    Arguments.GetStringImpl = ArgumentsCoreExtensions.GetStringCore;
    Arguments.GetSpreadTarget = ArgumentsCoreExtensions.GetSpreadTargetCore;
}
```

### TypeForwardedTo Attributes

7 new `TypeForwardedTo` entries added to Core `AssemblyInfo.cs`:

```csharp
[assembly: TypeForwardedTo(typeof(JSValue))]
[assembly: TypeForwardedTo(typeof(Arguments))]
[assembly: TypeForwardedTo(typeof(PropertyKey))]
[assembly: TypeForwardedTo(typeof(JSFunctionDelegate))]
[assembly: TypeForwardedTo(typeof(IElementEnumerator))]
[assembly: TypeForwardedTo(typeof(IJSPrototype))]
[assembly: TypeForwardedTo(typeof(IJSSymbol))]
```

Total TypeForwardedTo attributes in Core `AssemblyInfo.cs`: **21**.

### InternalsVisibleTo in Runtime

Runtime `AssemblyInfo.cs` grants `InternalsVisibleTo` to all satellite
assemblies that need access to `JSValue`'s internal factory fields:

```
Broiler.JavaScript.Core
Broiler.JavaScript.Core.Tests
Broiler.JavaScript.Clr
Broiler.JavaScript.Compiler
Broiler.JavaScript.Modules
Broiler.JavaScript.BuiltIns
Broiler.JavaScript.Debugger
```

### Build Fixes Required

Several build fixes were needed after the move:

1. **JSValueBuilder reflection** — Updated `PrototypeChain` field type from
   `JSPrototype` to `IJSPrototype` and super indexer parameter types from
   `typeof(JSObject)` to `typeof(JSValue)`.
2. **Ambiguous indexer casts** — `JSSymbol` satisfies both `IJSSymbol` and
   `JSValue` indexer overloads; disambiguated with explicit `(IJSSymbol)` casts.
3. **`IElementEnumerator` using directive** — Updated from
   `Broiler.JavaScript.Core.Enumerators` to `Broiler.JavaScript.Core.Core`.
4. **`InvokePropertyGetter` delegate** — Initially missing; caused
   `NullReferenceException` in prototype chain property access until wired.

### Architecture Impact

Runtime assembly now contains 14 source files (up from 8 before Phase 9b).
The assembly dependency graph remains acyclic:

```
Ast ← Storage ← Runtime ← Core ← {Compiler, Clr, Modules, BuiltIns, Debugger}
```

Runtime depends on:
- `Broiler.JavaScript.Ast` (for `IPropertyValue`, `IPropertyAccessor`, `StringSpan`)
- `Broiler.JavaScript.Storage` (for `KeyString`, `JSProperty`, `PropertySequence`, `ElementArray`)
- `Broiler.JavaScript.ExpressionCompiler` (for `YExpression<T>` in `IJSCompiler`)

### Test Results

All **998** tests pass across 10 test projects:
- Core: 641, Ast: 73, Parser: 78, Storage: 100, Debugger: 23, Clr: 29,
  Compiler: 9, Modules: 9, BuiltIns: 16, Runtime: 20.

### Remaining Work (Phase 9c+)

| Task | Status | Blocked By |
|------|--------|------------|
| Move `JSObject` to Runtime | ⏳ | ~500+ file references; `PropertySequence`/`ElementArray` usage |
| Move `JSFunction` to Runtime | ⏳ | Depends on `JSObject` move |
| Move `JSContext` to Runtime | ⏳ | Depends on `JSObject`/`JSFunction` moves |
| Move `JSPrototype`, `JSSymbol` to Runtime | ⏳ | Depends on `JSObject` move; currently implement `IJSPrototype`/`IJSSymbol` |
| Move `CoreScript` to Runtime | ⏳ | Depends on `JSContext` move |
| Move `IBuiltInRegistry` to Runtime | ⏳ | Depends on `JSContext` move |
| Move contract interfaces to Runtime | ✅ | `IDebugger`, `IClrInterop`, `IJSCompiler`, `ICodeCache`/`JSCode`/`JSCodeCompiler` moved (2026-03-21) |
| Additional BuiltIns extraction | ⏳ | Deep structural coupling |
| `InternalsVisibleTo` final cleanup | ⏳ | After all Phase 9 sub-phases complete |

## 23. Phase 9c — Contract Interface Migration (2026-03-21)

### Overview

Phase 9c begins the next stage of Runtime extraction by moving contract interfaces
that no longer depend on Core-only types. With `JSValue`, `Arguments`,
`PropertyKey`, and `JSFunctionDelegate` already in Runtime (Phase 9b), several
contract interfaces had their blockers resolved and could be migrated.

### Types Moved to Runtime

| Type | Lines | Original Location | Namespace | Purpose |
|------|-------|--------------------|-----------|---------|
| `IDebugger` | 28 | `Core/Debugger/` | `Broiler.JavaScript.Core.Debugger` | Debugger notification contract |
| `IClrInterop` | 46 | `Core/Core/Clr/` | `Broiler.JavaScript.Core.Core.Clr` | CLR ↔ JS marshalling contract |
| `IJSCompiler` | 27 | `Core/FastParser/Compiler/` | `Broiler.JavaScript.Core.FastParser.Compiler` | Source → expression tree compilation contract |
| `ICodeCache` | 12 | `Core/Emit/` | `Broiler.JavaScript.Core.Emit` | Compiled code caching contract |
| `JSCode` | 24 | `Core/Emit/` | `Broiler.JavaScript.Core.Emit` | Code unit struct (location + source + compiler) |
| `JSCodeCompiler` | 1 | `Core/Emit/` | `Broiler.JavaScript.Core.Emit` | Delegate: `() => YExpression<JSFunctionDelegate>` |

### Dependency Analysis

Each moved type was verified to depend only on types already available in Runtime
or its dependencies (Ast, Storage, ExpressionCompiler):

- **`IDebugger`** — references `JSValue` (Runtime) and primitive types only.
- **`IClrInterop`** — references `JSValue` (Runtime) and `System.Type`.
- **`IJSCompiler`** — references `YExpression<T>` (ExpressionCompiler),
  `JSFunctionDelegate` (Runtime), `StringSpan` (Ast), `ICodeCache` (moved together).
- **`ICodeCache`** + **`JSCode`** + **`JSCodeCompiler`** — references
  `JSFunctionDelegate` (Runtime), `StringSpan` (Ast),
  `YExpression<T>` (ExpressionCompiler).

### Resolved: `IBuiltInRegistry`

`IBuiltInRegistry` was initially blocked because its `Register(JSContext context)`
method directly referenced `JSContext`. This was resolved in Phase 9c+ by
introducing the `IJSContext` interface abstraction, allowing the contract to move
to Runtime without extracting the full `JSContext` class. See Section 24.

### TypeForwardedTo Attributes

6 new `TypeForwardedTo` entries added to Core `AssemblyInfo.cs`:

```csharp
[assembly: TypeForwardedTo(typeof(Broiler.JavaScript.Core.Debugger.IDebugger))]
[assembly: TypeForwardedTo(typeof(Broiler.JavaScript.Core.Core.Clr.IClrInterop))]
[assembly: TypeForwardedTo(typeof(Broiler.JavaScript.Core.Emit.ICodeCache))]
[assembly: TypeForwardedTo(typeof(Broiler.JavaScript.Core.Emit.JSCode))]
[assembly: TypeForwardedTo(typeof(Broiler.JavaScript.Core.Emit.JSCodeCompiler))]
[assembly: TypeForwardedTo(typeof(Broiler.JavaScript.Core.FastParser.Compiler.IJSCompiler))]
```

Total TypeForwardedTo attributes in Core `AssemblyInfo.cs`: **27**.

### Architecture Impact

Runtime assembly now contains **21** source files (up from 15 before Phase 9c).
The assembly dependency graph remains acyclic:

```
Ast ← Storage ← Runtime ← Core ← {Compiler, Clr, Modules, BuiltIns, Debugger}
```

Runtime now owns all cross-cutting contract interfaces:
- Module resolution: `IJSModuleResolver` (moved in earlier phase)
- Context: `IJSContext` (Phase 9c+)
- Built-in registration: `IBuiltInRegistry` (Phase 9c+)
- Debugging: `IDebugger`
- CLR interop: `IClrInterop`
- Compilation: `IJSCompiler`, `ICodeCache`, `JSCode`, `JSCodeCompiler`
- Attributes: `ExportAttribute`, `DefaultExportAttribute`

### Test Results

All **747** tests pass across 7 test projects:
- Core: 641, Clr: 29, Runtime: 20, BuiltIns: 16, Compiler: 9, Modules: 9,
  Debugger: 23.

### Remaining Work (Phase 9c continued)

| Task | Status | Blocked By | Action Plan |
|------|--------|------------|-------------|
| Move `JSObject` to Runtime | ⏳ Deferred | ~500+ file references; 22 subtypes cascade; see Section 26 | Section 29.5 |
| Move `JSFunction` to Runtime | ⏳ Deferred | Depends on `JSObject` move | Section 29.5 |
| Move `JSContext` to Runtime | ⏳ Deferred | Depends on `JSObject`/`JSFunction` moves | Section 29.5 |
| Move `JSPrototype`, `JSSymbol` to Runtime | ⏳ Deferred | Depends on `JSObject` move | Section 29.5 |
| Move `CoreScript` to Runtime | ✅ Complete | Completed in Phase 9d (2026-03-21) | — |
| Move `IBuiltInRegistry` to Runtime | ✅ Complete | Resolved in Phase 9c+ via `IJSContext` (Section 24) | — |
| Create `IJSFunction` interface | ✅ Complete | Completed in Phase 9d (2026-03-21) | — |
| Extend `IJSContext` with `CodeCache`/`WaitTask` | ✅ Complete | Completed in Phase 9d (2026-03-21) | — |
| Additional BuiltIns extraction | ✅ Complete | JSDisposableStack, JSDecimal, JSIntl extracted; remainder assessed (Section 31) | Section 29.2 |
| `InternalsVisibleTo` final cleanup | ✅ Complete | All migration bridges removed (Phase 10) | — |

### Open Questions and Design Clarifications

1. **`Bootstrap` class**: Referenced in the original issue but does not exist in
   the repository. Either it was removed in a prior refactoring or was a
   placeholder name. No action needed.

2. **`IBuiltInRegistry` migration path**: Resolved in Phase 9c+ by abstracting
   the parameter type to `IJSContext`. This proved to be a clean, low-disruption
   solution. See Section 24 for details.

3. **JSObject/JSFunction/JSContext extraction complexity**: These types are
   referenced by ~500+ files across Core, Compiler, Clr, BuiltIns, Modules, and
   Debugger assemblies. A staged approach using interface abstractions (similar to
   `IJSPrototype`/`IJSSymbol` from Phase 9b) and factory delegates is recommended.

### Revised Target Dates

| Milestone | Original Target | Revised Target | Status |
|-----------|----------------|----------------|--------|
| Phase 9a (Storage types) | — | 2026-03-21 | ✅ Complete |
| Phase 9b (Value types) | — | 2026-03-21 | ✅ Complete |
| Phase 9c (Contract interfaces) | — | 2026-03-21 | ✅ Complete |
| Phase 9c+ (IBuiltInRegistry unblocked) | — | 2026-03-21 | ✅ Complete |
| Phase 9d (CoreScript + IJSFunction + IJSContext enrichment) | — | 2026-03-21 | ✅ Complete |
| JSObject/JSFunction/JSContext concrete extraction | — | Deferred | ⏳ See Section 26 |
| Phase 10 (Final cleanup) | — | 2026-03-20 | ✅ Complete |

## 24. Phase 9c+ — IJSContext Abstraction and IBuiltInRegistry Migration (2026-03-21)

### Overview

Phase 9c+ resolves the `IBuiltInRegistry` blocker that was identified during
Phase 9c. Rather than waiting for the full `JSContext` extraction (which affects
~500+ files), this phase introduces a minimal `IJSContext` interface abstraction
in Runtime. This follows the same proven pattern used for `IJSPrototype` and
`IJSSymbol` in Phase 9b — creating a thin interface in Runtime to break the
dependency cycle, while the concrete implementation remains in Core.

### Approach: Interface Abstraction over Full Extraction

The key insight is that `IBuiltInRegistry.Register(JSContext context)` was
blocked solely because the parameter type `JSContext` lives in Core. By
introducing `IJSContext` (a minimal interface in Runtime), the contract can
reference a context without depending on the concrete class:

```csharp
// Runtime assembly — IJSContext.cs
public interface IJSContext : IDisposable
{
    long ID { get; }
}

// Runtime assembly — IBuiltInRegistry.cs (moved from Core)
public interface IBuiltInRegistry
{
    void Register(IJSContext context);
}
```

Core's `JSContext` implements `IJSContext`, and implementations of
`IBuiltInRegistry` in Core (e.g., `DefaultBuiltInRegistry`) cast the parameter
to `JSContext` when they need full access to the concrete type.

### Types Moved / Created

| Type | Action | Lines | Namespace | Purpose |
|------|--------|-------|-----------|---------|
| `IJSContext` | **Created** | 15 | `Broiler.JavaScript.Core.Core` | Minimal context abstraction for Runtime contracts |
| `IBuiltInRegistry` | **Moved** | 25 | `Broiler.JavaScript.Core.Core` | Built-in object registration contract |

### Changes Made

1. **Created `IJSContext` in Runtime** — Minimal interface with `ID` property
   and `IDisposable`. Follows the `IJSPrototype`/`IJSSymbol` pattern established
   in Phase 9b.

2. **Moved `IBuiltInRegistry` from Core to Runtime** — Changed parameter type
   from `JSContext` to `IJSContext`. Implementations that need the concrete type
   perform a downcast.

3. **Updated `JSContext`** — Now implements `IJSContext` in addition to
   `IDisposable`.

4. **Updated `DefaultBuiltInRegistry`** — `Register(IJSContext ctx)` casts to
   `JSContext` for full access. `AdditionalRegistrations` delegate retains its
   `Action<JSContext>` type for backward compatibility with satellite assemblies.

5. **Updated test code** — `DelegatingRegistry` in `BuiltInRegistryTests`
   updated to accept `IJSContext` parameter and cast to `JSContext`.

6. **Added `TypeForwardedTo` attributes** — 2 new entries for `IBuiltInRegistry`
   and `IJSContext` in Core `AssemblyInfo.cs`.

### TypeForwardedTo Attributes

2 new `TypeForwardedTo` entries added to Core `AssemblyInfo.cs`:

```csharp
[assembly: TypeForwardedTo(typeof(Broiler.JavaScript.Core.Core.IBuiltInRegistry))]
[assembly: TypeForwardedTo(typeof(Broiler.JavaScript.Core.Core.IJSContext))]
```

Total TypeForwardedTo attributes in Core `AssemblyInfo.cs`: **29**.

### Architecture Impact

Runtime assembly now contains **23** source files (up from 21 before Phase 9c+).
All cross-cutting contract interfaces have been successfully extracted to Runtime:

- Module resolution: `IJSModuleResolver`
- Debugging: `IDebugger`
- CLR interop: `IClrInterop`
- Compilation: `IJSCompiler`, `ICodeCache`, `JSCode`, `JSCodeCompiler`
- Context: `IJSContext`
- Built-in registration: `IBuiltInRegistry`
- Value types: `JSValue`, `Arguments`, `PropertyKey`, `JSFunctionDelegate`
- Type abstractions: `IJSPrototype`, `IJSSymbol`, `IElementEnumerator`
- Attributes: `ExportAttribute`, `DefaultExportAttribute`
- Utilities: `CancellableDisposableAction`, `StringExtensions`, `ObjectStatus`

The assembly dependency graph remains acyclic:

```
Ast ← Storage ← Runtime ← Core ← {Compiler, Clr, Modules, BuiltIns, Debugger}
```

### Lessons Learned

1. **Interface abstraction is the fastest unblocking strategy.** When a full type
   extraction involves hundreds of files, introducing a thin interface in the
   target assembly (and having the concrete type implement it in the source
   assembly) achieves the contract migration goal with minimal disruption.

2. **Downcast in implementations is acceptable.** `DefaultBuiltInRegistry`
   casting `IJSContext` to `JSContext` is a pragmatic trade-off. The alternative
   (a rich `IJSContext` interface exposing all of `JSContext`'s surface) would be
   premature abstraction that couples Runtime to Core's internal API shape.

3. **IJSContext scope should grow incrementally.** As more types move to Runtime
   (e.g., `CoreScript`), `IJSContext` can be extended with additional members
   (`Eval`, `WaitTask`, `CodeCache`) as needed, rather than specifying the full
   surface upfront.

### Test Results

All **747** tests pass across 7 test projects:
- Core: 641, Clr: 29, Debugger: 23, Runtime: 20, BuiltIns: 16, Compiler: 9,
  Modules: 9.

### Updated Remaining Work

| Task | Status | Notes | Action Plan |
|------|--------|-------|-------------|
| Move `IBuiltInRegistry` to Runtime | ✅ | Unblocked via `IJSContext` abstraction | — |
| Move contract interfaces to Runtime | ✅ | All contract interfaces now in Runtime | — |
| Move `CoreScript` to Runtime | ✅ | Completed in Phase 9d via factory delegates (2026-03-21) | — |
| Create `IJSFunction` interface | ✅ | Completed in Phase 9d; `JSFunction` implements it (2026-03-21) | — |
| Extend `IJSContext` interface | ✅ | `CodeCache` and `WaitTask` added in Phase 9d (2026-03-21) | — |
| Move `JSObject` to Runtime | ⏳ Deferred | ~500+ file references; 22 subtypes would cascade; see Section 26 | Section 29.5 |
| Move `JSFunction` to Runtime | ⏳ Deferred | Depends on `JSObject` move; concrete type stays in Core | Section 29.5 |
| Move `JSContext` to Runtime | ⏳ Deferred | Depends on `JSObject`/`JSFunction`; concrete type stays in Core | Section 29.5 |
| Additional BuiltIns extraction | ✅ Complete | JSDisposableStack, JSDecimal, JSIntl extracted; remainder assessed (Section 31) | Section 29.2 |
| `InternalsVisibleTo` final cleanup | ✅ Complete | All migration bridges removed (Phase 10) | — |

### Recommended Next Steps

1. **JSObject extraction (if pursued)**: Now that `IJSFunction` exists, JSObject
   could reference the interface instead of the concrete `JSFunction` class for
   constructor and prototype fields. However, this would still require moving
   22 subtypes and 500+ file references. See Section 26 for detailed assessment.

2. **Additional BuiltIns extraction**: ✅ Complete — JSDisposableStack, JSDecimal,
   and JSIntl family extracted to BuiltIns via factory delegates. Remaining
   candidates (JSArray, JSString, JSNumber, JSError, JSPromise, JSRegExp)
   assessed as impractical due to deep structural coupling. See Section 31.

3. **Coverage targets**: Focus on achieving ≥ 90% line coverage in each test
   project, now that the architecture is stable.

## 25. Phase 9d — CoreScript Extraction, IJSFunction, and IJSContext Enrichment (2026-03-21)

### Overview

Phase 9d completes the extraction of movable value type system code from Core
to Runtime and creates the interface abstractions needed for future work.

**Scope:**
1. Move `CoreScript` from Core to Runtime using factory delegates.
2. Create `IJSFunction` interface in Runtime.
3. Extend `IJSContext` with `CodeCache` and `WaitTask` properties.
4. Make `JSFunction` implement `IJSFunction`.
5. Extract `ExpressionHolder` to standalone file in Core.
6. Wire factory delegates via `[ModuleInitializer]` in Core.

### Types Moved / Created

| Type | From | To | Kind |
|------|------|----|------|
| `CoreScript` | Core | Runtime | Moved (factory-delegate-based) |
| `IJSFunction` | — | Runtime | New interface |
| `ExpressionHolder` | Core (was in `CoreScript.cs`) | Core (standalone file) | Extracted |

### CoreScript Factory Delegates

`CoreScript` in Runtime uses the following factory delegates, wired by
`CoreScriptCoreExtensions` in Core via `[ModuleInitializer]`:

| Delegate | Purpose | Core Implementation |
|----------|---------|---------------------|
| `CreateDefaultCompiler` | Creates default `IJSCompiler` | `() => new DefaultJSCompiler()` |
| `GetDefaultCodeCache` | Returns default `ICodeCache` | `() => DictionaryCodeCache.Current` |
| `GetCurrentContext` | Returns current context as `(JSValue, ICodeCache)` | `() => (JSContext.Current, Current?.CodeCache)` |
| `GetCurrentWaitTask` | Returns `WaitTask` from current context | `() => JSContext.Current?.WaitTask` |
| `CreateSyntaxError` | Creates a `SyntaxError` exception | `JSContext.NewSyntaxError(...)` |
| `RunAsyncPump` | Runs async operation with message pump | `AsyncPump.Run` |

### IJSContext Enrichment

The `IJSContext` interface was extended with two new properties:

```csharp
public interface IJSContext : IDisposable
{
    long ID { get; }
    ICodeCache CodeCache { get; }  // NEW — Phase 9d
    Task WaitTask { get; }         // NEW — Phase 9d
}
```

`JSContext` already had both members — `CodeCache` was converted from a public
field to a public property (auto-property with setter) to satisfy the interface
contract. `WaitTask` was already a read-only property.

### IJSFunction Interface

```csharp
public interface IJSFunction
{
    JSValue InvokeFunction(in Arguments a);
}
```

Follows the established pattern of `IJSPrototype` and `IJSSymbol` from Phase 9b.
`JSFunction` now implements `IJSFunction`, enabling Runtime-level code to invoke
functions through the interface without depending on the concrete Core type.

### TypeForwardedTo Attributes

Two new entries added to `Core/AssemblyInfo.cs` (31 total):

```csharp
[assembly: TypeForwardedTo(typeof(Broiler.JavaScript.Core.Core.IJSFunction))]
[assembly: TypeForwardedTo(typeof(Broiler.JavaScript.Core.CoreScript))]
```

### Architecture Impact

```
Runtime assembly (24 .cs files):
  JSValue, Arguments, PropertyKey, JSFunctionDelegate, IElementEnumerator,
  IJSPrototype, IJSSymbol, IJSContext, IJSFunction,                    ← NEW
  ObjectStatus, IJSModuleResolver, ExportAttribute, DefaultExportAttribute,
  CancellableDisposableAction, StringExtensions, IDebugger, IClrInterop,
  IJSCompiler, ICodeCache, JSCode, JSCodeCompiler, IBuiltInRegistry,
  CoreScript,                                                          ← MOVED
  AssemblyInfo

Core assembly:
  - CoreScript removed; TypeForwardedTo added
  - ExpressionHolder extracted to standalone file (stays in Core)
  - CoreScriptCoreExtensions added ([ModuleInitializer])
  - JSContext.CodeCache changed from field to property
  - JSFunction implements IJSFunction
  - 31 TypeForwardedTo attributes total
```

### Design Decisions

1. **CoreScript uses factory delegates, not IJSContext directly**: CoreScript
   needs the current context both as a `JSValue` (for `Arguments` construction)
   and for its `ICodeCache`. Since `IJSContext` is an interface and cannot be
   cast to `JSValue`, the factory delegate returns a `(JSValue, ICodeCache)`
   tuple. This avoids adding a `JSValue AsValue` member to `IJSContext`.

2. **ExpressionHolder stays in Core**: `ExpressionHolder` is used by
   `JSObjectBuilder` (a Core LinqExpressions class) and represents compiler
   expression tree data. It belongs in Core, not Runtime.

3. **Lazy compiler initialization**: `CoreScript.Compiler` uses lazy
   initialization (`??=`) via the `CreateDefaultCompiler` factory delegate,
   ensuring the delegate is called only once and only when needed.

### Test Results

All **998** tests pass across 10 test projects:
- Core: 641, Storage: 100, Parser: 78, Ast: 73, Clr: 29, Debugger: 23,
  Runtime: 20, BuiltIns: 16, Compiler: 9, Modules: 9.

### Remaining Work

See Section 26 for a comprehensive architectural assessment of the remaining
`JSObject`/`JSFunction`/`JSContext` concrete type extraction.

---

## 26. Architectural Assessment: JSObject/JSFunction/JSContext Extraction (2026-03-21)

### Summary

Moving `JSObject`, `JSFunction`, and `JSContext` concrete types from Core to
Runtime has been assessed and is **deferred** due to the massive scope and
cascade effects. This section documents the analysis, design decisions, and
recommended future approach.

### Current Architecture (Post-Phase 9d)

```
Broiler.JavaScript.Runtime (value type system layer):
├── JSValue (abstract base class)
├── Arguments, PropertyKey, JSFunctionDelegate (value primitives)
├── IJSPrototype, IJSSymbol, IJSContext, IJSFunction (interface abstractions)
├── CoreScript (high-level API via factory delegates)
├── All contract interfaces (IDebugger, IClrInterop, IJSCompiler, etc.)
└── Support types (ObjectStatus, IElementEnumerator, etc.)

Broiler.JavaScript.Core (implementation layer):
├── JSObject : JSValue (object base — 1,879 lines, 3 partial files)
├── JSFunction : JSObject, IJSFunction (function type)
├── JSContext : JSObject, IJSContext (execution context)
├── JSPrototype : IJSPrototype (prototype chain implementation)
├── JSSymbol : IJSSymbol (symbol implementation)
├── 22 types extending JSObject (JSArray, JSPromise, JSRegExp, etc.)
├── Error types, Promise infrastructure, Generator support
├── Built-in object implementations (partial — BuiltIns assembly)
└── Compiler integration (DefaultJSCompiler, DictionaryCodeCache)
```

### Why Concrete Types Remain in Core

1. **Cascade scope**: `JSObject` is extended by 22 types in Core and additional
   types in Clr, Network, and other assemblies. Moving `JSObject` would require
   all 22 subtypes to also move, effectively relocating ~60-80% of Core.

2. **Deep storage coupling**: `JSObject` directly uses `PropertySequence`,
   `ElementArray`, `JSProperty`, `KeyString`, `KeyStrings`, `SAUint32Map<T>`,
   `PropertyValueEnumerator`, `PropertyEnumerator`, and `ElementEnumerator`
   — most are internal Core types not designed for external consumption.

3. **Circular inheritance**: `JSFunction : JSObject` and `JSContext : JSObject`
   create mutual dependencies. Moving one requires moving all.

4. **Reference count**: `JSContext` is referenced in 196 files, `JSObject` in
   139 files, `JSFunction` in 113 files across the solution.

5. **Internal API surface**: Many `JSObject` members are `internal` and accessed
   by Core's built-in types, compiler integration, and error handling.

### What Has Been Achieved

Despite `JSObject`/`JSFunction`/`JSContext` remaining in Core, the value type
system is properly separated through the interface abstraction pattern:

| Concern | Runtime | Core |
|---------|---------|------|
| Value base class | ✅ `JSValue` | — |
| Function invocation | ✅ `IJSFunction` | `JSFunction` implements |
| Context contract | ✅ `IJSContext` | `JSContext` implements |
| Prototype chain | ✅ `IJSPrototype` | `JSPrototype` implements |
| Symbol identity | ✅ `IJSSymbol` | `JSSymbol` implements |
| Compilation API | ✅ `CoreScript` | Factories in Core |
| All contracts | ✅ In Runtime | Implementations in Core |

This means **satellite assemblies can interact with the value type system
through Runtime interfaces without depending on Core**, which was the primary
architectural goal.

### Recommended Future Approach

If full extraction is desired in the future, the recommended approach is:

1. **Create `Broiler.JavaScript.ObjectModel` assembly**: A new assembly between
   Runtime and Core containing `JSObject`, `JSFunction`, `JSContext`, and the
   22 subtypes. This avoids bloating Runtime while achieving the separation.

2. **Incremental subtype extraction**: Move subtypes one at a time to
   ObjectModel, starting with leaf types (e.g., `JSDate`, `JSMath`) that have
   few dependencies.

3. **Internal-to-public API audit**: Identify which `internal` members on
   `JSObject` are accessed by satellite assemblies and either make them
   `public` or provide interface abstractions.

4. **Estimated effort**: 500+ file reference updates, 3-5 engineering days,
   high risk of regression without comprehensive test coverage.

### Conclusion

The current architecture achieves the primary goal of the refactor: **clean
separation of concerns with unidirectional dependencies and no circular
references**. The Runtime assembly contains all base types, contracts, and
interfaces needed by satellite assemblies. The concrete implementation types
remain in Core as an intentional architectural decision, not a limitation.
**Section 29.5 provides a detailed action plan** with preconditions and subtasks
if this extraction is pursued in the future.

---

## 27. Phase 6 Continued — BuiltIns Extraction Blocker Analysis (2026-03-21)

### Overview

A thorough analysis of all remaining built-in types in Core was performed to
identify candidates for extraction to the BuiltIns assembly. This analysis was
conducted after all Phase 9 (9a–9d) and Phase 10 API changes were complete,
ensuring the assessment reflects the current API surface.

### Key Finding

**No additional built-in types can be extracted to BuiltIns without
architectural changes to Core or Compiler.** The remaining types are blocked
by **dependency-direction coupling**, not by access-modifier visibility issues.
Phase 10's public API changes resolved all visibility blockers, but the
fundamental issue is that Core and Compiler assemblies directly reference
these types — moving them to BuiltIns would create Core → BuiltIns and
Compiler → BuiltIns reverse dependencies, violating the unidirectional
dependency graph.

### Candidate Analysis

| Type | Location | Referenced By | Blocker | Extractable |
|------|----------|---------------|---------|-------------|
| **JSDisposableStack** | `Core/Disposable/` | Compiler: `FastFunctionScope.cs`, `FastCompiler.VisitProgram.cs`, `FastCompiler.VisitVariableDeclaration.cs` (3 files, expression tree emission for `using` declarations) | Compiler → BuiltIns reverse dependency | ❌ |
| **JSSuppressedError** | `Core/Disposable/` | `JSDisposableStack.cs` (internal only) | Blocked by JSDisposableStack | ❌ |
| **JSIntl** | `Core/Intl/` | Core: `JSGlobal.cs` (type registration via `ClrInterop.GetClrType`) | Core → BuiltIns reverse dependency | ❌ |
| **JSIntlDateTimeFormat** | `Core/Intl/` | Core: `JSDatePrototype.cs` (date formatting via `JSIntlDateTimeFormat.Get`) | Core → BuiltIns reverse dependency | ❌ |
| **JSIntlRelativeTimeFormat** | `Core/Intl/` | `JSIntl.cs` (internal only) | Blocked by JSIntl | ❌ |
| **JSDecimal** | `Core/Decimal/` | Core: `JSMath.cs` (6 methods with `is JSDecimal` type checks), `JSBigIntBuilder.cs` (expression builder); Compiler: `FastCompiler.VisitLiteral.cs` | Core + Compiler → BuiltIns reverse dependency | ❌ |
| **DataView** | `Core/DataView/` | Internal: `JSArrayBuffer.buffer` field access; Network: `FetchResponse.cs` | Internal field access + Core → BuiltIns reverse dependency | ❌ |
| **JSJSON** | `Core/Json/` | Core: `Names.g.cs` (generated registration); Network: `FetchResponse.cs` (`JSJSON.Parse` call) | Core → BuiltIns reverse dependency | ❌ |
| **JSProxy** | `Core/Proxy/` | Core: overrides `protected internal` `GetValue`/`SetValue` on `JSObject` | Access modifier semantics change across assemblies | ❌ |
| **JSArray** | `Core/Array/` | 13 type checks across Core | Core → BuiltIns reverse dependency | ❌ |
| **JSString** | `Core/String/` | 8 type checks across Core | Core → BuiltIns reverse dependency | ❌ |
| **JSNumber** | `Core/Number/` | Static property access from JSMath/JSDatePrototype | Core → BuiltIns reverse dependency | ❌ |
| **JSError** | `Core/Error/` | Inheritance chain, type checks from JSException | Core → BuiltIns reverse dependency | ❌ |
| **JSPromise** | `Core/Promise/` | Stored in JSContext, property access | Core → BuiltIns reverse dependency | ❌ |
| **JSRegExp** | `Core/RegExp/` | 7 type checks from JSStringPrototype | Core → BuiltIns reverse dependency | ❌ |

### Already Extracted (for reference)

| Type | Extracted To | Date | Notes |
|------|-------------|------|-------|
| **EventTarget** | BuiltIns | 2026-03-20 | No Core/Compiler back-references |
| **Event** | BuiltIns | 2026-03-20 | No Core/Compiler back-references |
| **CustomEvent** | BuiltIns | 2026-03-20 | No Core/Compiler back-references |
| **DomEventHandler** | BuiltIns | 2026-03-20 | No Core/Compiler back-references |
| **JSWeakRef** | BuiltIns | 2026-03-20 | No Core/Compiler back-references |
| **JSFinalizationRegistry** | BuiltIns | 2026-03-20 | No Core/Compiler back-references |

### Recommended Future Approach for Unblocking

To enable further BuiltIns extraction, the following architectural changes would
be needed:

1. **Factory delegate pattern** (proven in Phase 9d): Replace direct type
   references in Core/Compiler with factory delegates wired via
   `[ModuleInitializer]`. For example, `JSMath` could use a
   `Func<decimal, JSValue> CreateDecimal` delegate instead of
   `new JSDecimal(value)`. This is the same pattern used successfully for
   `CoreScript` in Phase 9d.

2. **Interface abstraction pattern** (proven in Phase 9b/9c): Create interfaces
   like `IJSDisposableStack` in Runtime, referenced by Compiler. The BuiltIns
   assembly provides the concrete implementation. This mirrors the
   `IJSContext`/`IJSFunction` pattern.

3. **Registration-based type checks**: Replace `is JSDecimal`, `is JSIntl`, etc.
   type checks with registered type identity checks via a central type registry.
   This removes the need for compile-time type references.

4. **Estimated effort per type**: Each type requires 1-3 hours for factory
   delegate introduction, interface creation, and test updates. The full
   extraction of all remaining types would require 3-5 engineering days due to
   the cascade effects.

### Conclusion

Phase 6 BuiltIns extraction has reached its practical limit with the current
architecture. The 6 types already extracted (Events + Weak) represent all types
that had zero back-references from Core or Compiler. Further extraction requires
architectural changes (factory delegates, interface abstractions) that are
individually feasible but collectively represent significant refactoring effort.
**Section 29.2 provides a prioritized action plan** for the next batch of
extractions (JSDisposableStack, JSDecimal, JSIntl), starting with the
lowest-effort candidates.

---

## 28. Current State Summary (2026-03-21)

### Assembly Architecture

```
Broiler.JavaScript.Runtime (24 source files):
├── Value types: JSValue, Arguments, PropertyKey, JSFunctionDelegate
├── Interface abstractions: IJSPrototype, IJSSymbol, IJSContext, IJSFunction
├── Contract interfaces: IDebugger, IClrInterop, IJSCompiler, ICodeCache,
│   IBuiltInRegistry, IJSModuleResolver
├── High-level API: CoreScript (factory-delegate-based)
├── Support types: ObjectStatus, IElementEnumerator, JSCode, JSCodeCompiler,
│   CancellableDisposableAction, StringExtensions
├── Module markers: ExportAttribute, DefaultExportAttribute
└── AssemblyInfo (31 TypeForwardedTo attributes in Core)

Broiler.JavaScript.Storage (storage types):
├── JSProperty, PropertySequence, ElementArray (interface-typed fields/params)
├── KeyString, KeyStrings, KeyType
├── SAUint32Map<T>, PropertyMap, and other hash map types
└── JSPropertyAttributes

Broiler.JavaScript.Core (implementation layer):
├── JSObject, JSFunction, JSContext (concrete types)
├── JSPrototype, JSSymbol (interface implementations)
├── 22+ types extending JSObject
├── Built-in type implementations (Array, String, Number, etc.)
├── Compiler integration (DefaultJSCompiler, DictionaryCodeCache)
└── Factory delegate wiring via [ModuleInitializer]

Satellite assemblies (all → Core, no reverse deps):
├── Broiler.JavaScript.BuiltIns (12 types: Events, Weak, Disposable, Decimal, Intl)
├── Broiler.JavaScript.Compiler (AST → LINQ expression trees)
├── Broiler.JavaScript.Clr (.NET ↔ JS bridging)
├── Broiler.JavaScript.Modules (ES module system)
├── Broiler.JavaScript.ModuleExtensions (fluent module-registration extensions)
├── Broiler.JavaScript.Debugger (V8 Inspector Protocol)
├── Broiler.JavaScript.Parser (Lexer + Parser)
└── Broiler.JavaScript.Ast (AST node types)
```

### Test Matrix

| Test Project | Tests | Status |
|-------------|-------|--------|
| Core.Tests | 641 | ✅ Pass |
| Storage.Tests | 100 | ✅ Pass |
| Parser.Tests | 78 | ✅ Pass |
| Ast.Tests | 73 | ✅ Pass |
| Clr.Tests | 29 | ✅ Pass |
| BuiltIns.Tests | 29 | ✅ Pass |
| Debugger.Tests | 23 | ✅ Pass |
| Runtime.Tests | 20 | ✅ Pass |
| ModuleExtensions.Tests | 13 | ✅ Pass |
| Compiler.Tests | 9 | ✅ Pass |
| Modules.Tests | 9 | ✅ Pass |
| **Total** | **1024** | **✅ All pass** |

### Phase Completion Summary

| Phase | Assembly | Status | Completion | Next Steps |
|-------|----------|--------|------------|------------|
| 1 | Ast | ✅ Complete | 2026-03-19 | — |
| 2 | Parser | ✅ Complete | 2026-03-19 | — |
| 3 | Storage | ✅ Complete | 2026-03-21 | — |
| 4 | Debugger | ✅ Complete | 2026-03-19 | — |
| 5 | Clr | ✅ Complete | 2026-03-20 | — |
| 6 | BuiltIns | ✅ Complete | 2026-03-21 | Extracted 12 types via factory delegates; remaining candidates assessed as impractical (Section 31) |
| 7 | Compiler | ✅ Complete | 2026-03-20 | — |
| 8 | Modules | ✅ Complete | 2026-03-20 | — |
| 9a | Storage types | ✅ Complete | 2026-03-21 | — |
| 9b | Value types | ✅ Complete | 2026-03-21 | — |
| 9c | Contract interfaces | ✅ Complete | 2026-03-21 | — |
| 9c+ | IJSContext abstraction | ✅ Complete | 2026-03-21 | — |
| 9d | CoreScript + IJSFunction | ✅ Complete | 2026-03-21 | — |
| 10 | Cleanup | ✅ Complete | 2026-03-20 | — |
| — | ModuleExtensions | ✅ Complete | 2026-03-21 | Namespace aligned, bugs fixed, 13-test project created (Section 30) |
| — | Coverage | ⏳ Measuring | — | P1: Review reports, enforce thresholds (Section 29.1) |
| — | Integration tests | 📋 Planned | — | P2: Create Integration.Tests project (Section 29.3) |
| — | ObjectModel | ⏳ Deferred | — | P3: If needed, extract JSObject hierarchy (Section 29.5) |

### Open Items

| Item | Status | Priority | Notes |
|------|--------|----------|-------|
| Phase 6 continued extraction | ✅ Complete | P2 | JSDisposableStack, JSDecimal, JSIntl extracted; remaining candidates assessed as impractical (Section 31) |
| JSObject/JSFunction/JSContext extraction | ⏳ Deferred | P3 | See Section 26; requires ObjectModel assembly; not blocking any current work |
| `WebAtoms.XF` InternalsVisibleTo | ⏳ External | P4 | Cannot remove without external coordination; document deprecation timeline |
| Coverage ≥ 90% per assembly | ⏳ Scheduled | P1 | `coverlet.collector` integrated; CI collects data; review and improve per Section 29 |
| Integration test project | 📋 Planned | P2 | `Broiler.JavaScript.Integration.Tests` mentioned in Section 6.5 but not yet created |
| Downstream consumer migration docs | ⏳ Scheduled | P2 | Section 11 needs verification pass; unchecked item from Phase 9b checklist |

---

## 29. Future and Deferred Work — Action Plan (2026-03-21)

### Overview

This section consolidates all future, deferred, and blocked items from the
refactor roadmap into an actionable plan with prioritized subtasks, milestones,
and clear ownership guidance. Each item is broken into concrete next steps so
that any contributor can pick up the work.

**Completed items tracked here:**
- ✅ **P1 — ModuleExtensions alignment** (verified 2026-03-21):
  - [x] Namespace alignment: both source files (`JSModuleContextExtension.cs`,
    `ModuleBuilder.cs`) updated from `YantraJS.ModuleExtensions` / global
    namespace to `Broiler.JavaScript.ModuleExtensions`.
  - [x] TFM alignment: `net8.0` target framework confirmed in both
    `Broiler.JavaScript.ModuleExtensions.csproj` and
    `Broiler.JavaScript.ModuleExtensions.Tests.csproj`.
  - [x] Reference alignment: project references validated — main project
    depends on Core, Modules, Clr, ExpressionCompiler; test project depends
    on ModuleExtensions, Core, Clr, Compiler, Modules.
  - [x] Bug fixes applied and verified:
    - `ExportType(Type, string?)` respects `name` parameter (`name ?? type.Name`).
    - Duplicate assignment in `AddModuleToContext` removed.
    - `ImportModule` uses public `All` property instead of private-field reflection.
    - Unused `System.Reflection` import removed.
  - [x] Project file cleanup: duplicate `LangVersion`, contradictory
    `GeneratePackageOnBuild`, stale URLs and `PackageVersion` removed;
    collection fields made `readonly`.
  - [x] Dedicated test project created: 13 tests (9 `ModuleBuilderTests`,
    4 `JSModuleContextExtensionTests`) covering all public API surface.
  - [x] CI integration: `ModuleExtensions.Tests` added as 11th test step
    in `.github/workflows/ci.yml` with `coverlet` coverage collection.
  - [x] Solution inclusion: both projects registered in `YantraJS.sln`.
  - See Section 30 for full implementation details.
- ✅ **P2 — BuiltIns extraction via factory delegates** (completed 2026-03-21):
  - JSDisposableStack, JSDecimal, JSIntl extracted; remaining candidates
    assessed as impractical. See Section 29.2 and Section 31 for details.

### Priority Definitions

| Priority | Meaning | Timeline |
|----------|---------|----------|
| **P1** | High — enables quality/correctness goals | Next milestone |
| **P2** | Medium — improves architecture, unblocks future work | 1–2 milestones |
| **P3** | Low — nice-to-have, large scope, no current blocker | Future, as capacity allows |
| **P4** | External — depends on third-party coordination | When external parties respond |

---

### 29.1 P1 — Test Coverage Improvement

**Current state:** 11 extracted/satellite assemblies have dedicated test projects
(1024 tests total). `coverlet.collector` is integrated into CI, but coverage
thresholds have not been enforced and coverage reports have not been reviewed
per-assembly.

**Goal:** ≥ 90% line coverage per extracted assembly (Success Criterion #2).

**Actionable subtasks:**

- [ ] Review `coverlet` CI output and generate per-assembly coverage reports.
- [ ] Identify assemblies below 90% line coverage and prioritize gap areas.
- [ ] Add tests for uncovered branches in Runtime assembly (currently 20 tests
  covering 24 source files — likely below 90%).
- [ ] Add tests for uncovered branches in BuiltIns assembly (currently 29 tests
  covering 12 source files).
- [ ] Add tests for uncovered branches in Compiler assembly (currently 9 tests).
- [ ] Add tests for uncovered branches in Modules assembly (currently 9 tests).
- [ ] Add tests for uncovered branches in ModuleExtensions assembly (currently
  13 tests covering 2 source files).
- [ ] Configure CI to fail on coverage regression (optional — add
  `coverlet` threshold configuration to test projects or CI workflow).
- [ ] Update Success Criteria table (Section 8) when ≥ 90% is achieved.

**Validation:** CI coverage report shows ≥ 90% line coverage for each assembly.

---

### 29.2 P2 — Phase 6 Continued: BuiltIns Extraction via Factory Delegates

**Current state:** 6 types extracted (EventTarget, Event, CustomEvent,
DomEventHandler, JSWeakRef, JSFinalizationRegistry). Remaining types blocked
by dependency-direction coupling (Section 27). Two proven patterns exist:
factory delegates (Phase 9d) and interface abstractions (Phase 9b/9c).

**Goal:** Extract additional built-in types to BuiltIns assembly by replacing
direct type references in Core/Compiler with factory delegates or interfaces.

**Actionable subtasks (ordered by estimated effort, lowest first):**

- [x] **JSDisposableStack** ✅ (completed 2026-03-21):
  - Created `IJSDisposableStack` interface in Runtime with static factory delegate.
  - Replaced 3 direct references in Compiler (`FastFunctionScope.cs`,
    `FastCompiler.VisitProgram.cs`, `FastCompiler.VisitVariableDeclaration.cs`)
    with `IJSDisposableStack` interface and `IJSDisposableStack.New()` factory.
  - Wired factory via `[ModuleInitializer]` in BuiltIns.
  - Moved `JSDisposableStack` and `JSSuppressedError` to BuiltIns/Disposable/.
  - Added tests to BuiltIns.Tests.

- [x] **JSDecimal** ✅ (completed 2026-03-21):
  - Added `IsDecimal`/`DecimalValue` virtual properties to `JSValue` in Runtime.
  - Added `CreateDecimal`/`CreateDecimalFromString` factory delegates to `JSValue`.
  - Replaced 6 `is JSDecimal` type checks in `JSMath.cs` with `IsDecimal`/`DecimalValue`.
  - Replaced `JSDecimalBuilder.New()` in Compiler with factory delegate call.
  - Wired factory via `[ModuleInitializer]` in BuiltIns.
  - Moved `JSDecimal` + `JSDecimalExtensions` to BuiltIns/Decimal/.
  - Added tests to BuiltIns.Tests.

- [x] **JSIntl / JSIntlDateTimeFormat / JSIntlRelativeTimeFormat**
  ✅ (completed 2026-03-21):
  - Added `IntlFactory` delegate to `JSGlobalStatic`.
  - Added `IntlDateFormatter` delegate to `JSDate`.
  - Wired factories via `[ModuleInitializer]` in BuiltIns.
  - Moved all three types to BuiltIns/Intl/.
  - Added tests to BuiltIns.Tests.

- [x] **Remaining BuiltIns candidates** ✅ (assessed 2026-03-21):
  - Audited JSArray (12 type checks), JSString (16), JSNumber (15), JSError (6),
    JSPromise (6), JSRegExp (8), JSProxy (0), JSJSON (0), JSDataView (0).
  - Types with high reference counts (JSArray, JSString, JSNumber) are deeply
    coupled throughout Core arithmetic, comparison, and conversion logic.
    Extraction would require 40+ virtual method overrides and risk performance
    regressions in hot paths.
  - JSProxy, JSJSON, JSDataView have zero `is` type checks in Core/Compiler
    but are referenced by name in source-generated registration code (`Names.g.cs`),
    making extraction require changes to the source generator itself.
  - **Conclusion:** All practical extraction candidates have been completed.
    Remaining types are impractical to extract without disproportionate effort
    and risk. See Section 31 for detailed analysis.

**Validation:** Each extracted type compiles independently in BuiltIns; all 1024
tests pass; no reverse dependencies (Core/Compiler → BuiltIns) introduced. ✅

**Milestone target:** ✅ Complete. JSDisposableStack, JSDecimal, and JSIntl
extracted via factory delegates. Remaining candidates assessed as impractical.

---

### 29.3 P2 — Integration Test Project

**Current state:** Section 6.5 mentions a `Broiler.JavaScript.Integration.Tests`
project for tests that span multiple assemblies, but it has not been created.
Currently, cross-assembly integration is validated through `Core.Tests` (641
tests).

**Goal:** Create a dedicated integration test project to validate end-to-end
behavior across assembly boundaries.

**Actionable subtasks:**

- [ ] Create `Broiler.JavaScript.Integration.Tests` project referencing all
  engine assemblies (Core, Runtime, Storage, Compiler, Modules, BuiltIns, Clr,
  Debugger).
- [ ] Add to `Broiler.slnx` and CI workflow.
- [ ] Migrate or duplicate key integration-style tests from `Core.Tests` that
  exercise cross-assembly boundaries (e.g., script compilation → execution →
  built-in object interaction).
- [ ] Add tests for module initializer registration ordering.
- [ ] Add tests for factory delegate wiring (ensure `CoreScript` delegates,
  `JSValue` delegates, `PropertySequence` delegates are correctly wired).
- [ ] Add tests for `TypeForwardedTo` backward compatibility (ensure types
  resolve correctly through Core even though they live in Runtime/Storage).

**Validation:** Integration.Tests project compiles and all tests pass on all
3 CI platforms.

---

### 29.4 P2 — Downstream Consumer Migration Documentation

**Current state:** Section 11 documents migration steps for downstream consumers.
`Broiler.App` and `Broiler.Cli` have been updated to use the `All` meta-package.
However, the documentation has not been verified end-to-end after Phase 9d
changes.

**Actionable subtasks:**

- [ ] Verify Section 11 migration steps are accurate for current assembly
  structure (post-Phase 9d).
- [ ] Update consumer analysis table with current reference state.
- [ ] Add migration notes for consumers that use `CoreScript` API (factory
  delegates must be initialized via module initializer).
- [ ] Document `TypeForwardedTo` behavior: consumers referencing types via Core
  namespace continue to work transparently.
- [ ] Add troubleshooting guide for common migration issues (e.g., missing
  module initializer registration, `FileNotFoundException` for satellite
  assemblies).

**Validation:** A clean consumer project can build against the refactored
assembly structure following only the documented steps.

---

### 29.5 P3 — JSObject/JSFunction/JSContext Extraction (ObjectModel Assembly)

**Current state:** Concrete types intentionally remain in Core per architectural
assessment (Section 26). The primary refactor goal (clean separation via
interfaces) has been achieved. This item is deferred, not blocked.

**Goal:** If pursued, create `Broiler.JavaScript.ObjectModel` assembly as an
intermediate layer between Runtime and Core.

**Preconditions (must be met before starting):**

- [ ] P1 coverage targets achieved (to ensure regression safety).
- [ ] P2 integration test project created (to validate cross-assembly behavior).
- [ ] P2 BuiltIns extraction proof-of-concept completed (validates factory
  delegate pattern at scale).

**Actionable subtasks (if/when pursued):**

- [ ] Create `Broiler.JavaScript.ObjectModel` project.
- [ ] Audit all `internal` members on `JSObject` accessed by satellite
  assemblies; make public or provide interface abstractions.
- [ ] Move leaf subtypes first (e.g., `JSDate`, `JSMath`) to ObjectModel as a
  pilot.
- [ ] Move `JSObject` base class to ObjectModel.
- [ ] Move `JSFunction` and `JSContext` to ObjectModel.
- [ ] Move remaining 22 subtypes to ObjectModel.
- [ ] Update all 500+ file references.
- [ ] Add `TypeForwardedTo` attributes in Core for all moved types.
- [ ] Run full test suite; verify zero regressions.

**Estimated effort (as of 2026-03-21):** 3–5 engineering days, high regression risk.

**Decision criteria:** Only pursue if a concrete downstream need arises (e.g.,
a consumer needs to depend on `JSObject` without pulling in all of Core).
Current interface-based architecture satisfies all known requirements.

---

### 29.6 P4 — WebAtoms.XF InternalsVisibleTo Removal

**Current state:** `WebAtoms.XF` is an external consumer with an
`InternalsVisibleTo` entry in Core's `AssemblyInfo.cs`. Cannot be removed
without external coordination.

**Actionable subtasks:**

- [ ] Contact `WebAtoms.XF` maintainers to discuss migration timeline.
- [ ] Provide migration guide listing all internal APIs used by `WebAtoms.XF`
  and their public replacements (many were already made public in Phase 10).
- [ ] Set deprecation timeline (e.g., 2 release cycles).
- [ ] After confirmation from external team, remove `InternalsVisibleTo` entry.
- [ ] Verify `WebAtoms.XF` builds against public API only.

**Decision criteria:** External coordination required. If no response within
deprecation timeline, document the entry as permanently retained with
justification.

---

### 29.7 Deferred Design Decisions

The following design decisions were identified during the refactor but are not
currently actionable. They are documented here for future reference.

| Decision | Context | Current Status | When to Revisit |
|----------|---------|----------------|-----------------|
| Per-type BuiltIns assemblies (e.g., `BuiltIns.Promise`) | Section 3.6 — each built-in folder is self-contained | Not planned | Only if consumers need fine-grained dependency control |
| Registration-based type identity checks | Section 27 — alternative to `is JSDecimal` etc. | Design proposed, not implemented | When BuiltIns extraction requires replacing type checks at scale |
| `Bootstrap` class creation | Section 23 — does not exist in repo | Not needed | N/A — `IBuiltInRegistry` + `DefaultBuiltInRegistry` handles bootstrap |
| Compiler factory delegate refactor | Section 27 — replace direct type refs in Compiler | ✅ Proven and applied | Applied for `JSDisposableStack` (Section 31); pattern available for future use |

---

### 29.8 Milestone Summary

| Milestone | Items | Target | Status |
|-----------|-------|--------|--------|
| **P1 — ModuleExtensions** | Namespace/TFM/reference alignment, bug fixes, test project (Section 30) | — | ✅ Complete (verified 2026-03-21) |
| **P2 — BuiltIns extraction** | JSDisposableStack, JSDecimal, JSIntl via factory delegates (Section 31) | — | ✅ Complete (2026-03-21) |
| **P1 — Coverage improvement** | Per-assembly ≥ 90% line coverage (Section 29.1) | Next PR cycle | 📋 Planned |
| **P2 — Integration tests** | `Broiler.JavaScript.Integration.Tests` project (Section 29.3) | 1–2 PR cycles | 📋 Planned |
| **P2 — Docs update** | Downstream consumer migration documentation (Section 29.4) | 1–2 PR cycles | 📋 Planned |
| **P3 — ObjectModel** | JSObject/JSFunction/JSContext extraction (Section 29.5) | When preconditions met | ⏳ Deferred |
| **External** | P4: WebAtoms.XF coordination | When external parties respond | ⏳ External |

---

## 30. ModuleExtensions — Fluent Module-Registration Extensions (2026-03-21)

This section documents the completion of the `Broiler.JavaScript.ModuleExtensions`
assembly alignment as part of the JavaScript engine assembly refactor.

### Background

The ModuleExtensions project provides a fluent API for registering JavaScript
modules from C# code. It was originally created under the `YantraJS.ModuleExtensions`
namespace with no tests. During Phase 10 (Section 19), project references and
TFM were updated, but source-level namespace alignment, bug fixes, and test
coverage were deferred.

### Changes Made

#### 1. Namespace Alignment

Both source files updated to use the `Broiler.JavaScript.ModuleExtensions`
namespace, matching the assembly naming convention used by all other refactored
assemblies:

| File | Before | After |
|------|--------|-------|
| `JSModuleContextExtension.cs` | `YantraJS.ModuleExtensions` | `Broiler.JavaScript.ModuleExtensions` |
| `ModuleBuilder.cs` | _(no namespace — global)_ | `Broiler.JavaScript.ModuleExtensions` |

#### 2. Bug Fixes

| Issue | File | Fix |
|-------|------|-----|
| `ExportType(Type, string?)` ignored `name` parameter — always used `type.Name` | `ModuleBuilder.cs` | Changed to `name ?? type.Name` (consistent with generic overload) |
| Duplicate assignment `globalExport[name] = globalExport[name] = jsValue;` | `ModuleBuilder.cs` | Removed redundant assignment |
| `ImportModule` used fragile reflection to access private `moduleCache` field | `JSModuleContextExtension.cs` | Replaced with public `All` property lookup via LINQ |
| Removed unused `System.Reflection` import | `JSModuleContextExtension.cs` | Cleaned up imports |

#### 3. Project File Cleanup

| Issue | Fix |
|-------|-----|
| Duplicate `LangVersion` declarations (`default` and `latest`) | Consolidated to single `latest` |
| Contradictory `GeneratePackageOnBuild=true` with `IsPackable=false` | Removed `GeneratePackageOnBuild` |
| Stale `PackageProjectUrl` and `RepositoryUrl` pointing to old yantrajs repo | Removed stale URLs |
| Stale `PackageVersion` (ignored when `IsPackable=false`) | Removed |
| Made collection fields `readonly` | `exportedObjects` and `_moduleName` |

#### 4. Test Project Created

Created `Broiler.JavaScript.ModuleExtensions.Tests` with 13 tests covering:

| Test Class | Tests | Coverage |
|-----------|-------|----------|
| `ModuleBuilderTests` | 9 | `ExportType<T>` (default/custom name), `ExportType(Type)` (default/custom name), `ExportValue`, `ExportFunction`, fluent chaining, default export, callable function |
| `JSModuleContextExtensionTests` | 4 | `CreateModule`, `ImportModule` success, `ImportModule` not found, export value verification |

### Architecture Impact

No architectural changes. The ModuleExtensions assembly continues to depend on
Core, Clr, ExpressionCompiler, and Modules. Internal APIs remain unchanged;
only the public namespace and bug fixes affect consumers.

### Test Results

All **1024** tests pass across 11 test projects:
- Core: 641, Ast: 73, Parser: 78, Storage: 100, Debugger: 23, Clr: 29,
  Compiler: 9, Modules: 9, **BuiltIns: 29**, Runtime: 20, **ModuleExtensions: 13**.

---

## 31. Phase 6 Continued — BuiltIns Extraction Completion (2026-03-21)

This section documents the completion of the Phase 6 BuiltIns extraction work
outlined in Section 29.2, including the extraction of JSDisposableStack,
JSSuppressedError, JSDecimal, and JSIntl family types, and the assessment of
remaining candidates.

### Background

Section 27 identified that remaining BuiltIns candidates were blocked by
dependency-direction coupling — Core and Compiler referenced these types
directly. Section 29.2 proposed factory delegates and interface abstractions
as the solution. This work implements that plan.

### Changes Made

#### 1. IJSDisposableStack Interface (Runtime)

Created `IJSDisposableStack` interface in the Runtime assembly with:
- `AddDisposableResource(JSValue, bool)` method
- `Dispose()` method returning `JSValue`
- Static `CreateNew` factory delegate (wired by BuiltIns `[ModuleInitializer]`)
- Static `New()` convenience method

**Compiler changes:**
- `FastFunctionScope.cs`: `Disposable` parameter type changed from
  `typeof(JSDisposableStack)` to `typeof(IJSDisposableStack)`
- `FastCompiler.VisitProgram.cs`: `Expression.New(scope.Disposable.Type)` replaced
  with `StaticCallExpression<IJSDisposableStack>(() => () => IJSDisposableStack.New())`
- `FastCompiler.VisitVariableDeclaration.cs`: 3 `CallExpression<JSDisposableStack, ...>`
  calls changed to `CallExpression<IJSDisposableStack, ...>`

#### 2. JSDisposableStack + JSSuppressedError (Core → BuiltIns)

- Moved `JSDisposableStack.cs` to `BuiltIns/Disposable/JSDisposableStack.cs`
- Moved `JSSuppressedError.cs` to `BuiltIns/Disposable/JSSuppressedError.cs`
- `JSDisposableStack` now implements `IJSDisposableStack`
- Added `InternalsVisibleTo("Broiler.JavaScript.BuiltIns")` in Core AssemblyInfo

#### 3. JSDecimal (Core → BuiltIns)

**JSValue additions (Runtime):**
- `IsDecimal` virtual property (default `false`, overridden to `true` in JSDecimal)
- `DecimalValue` virtual property (throws by default, overridden in JSDecimal)
- `CreateDecimalFactory` / `CreateDecimalFromStringFactory` factory delegates
- `CreateDecimal(decimal)` / `CreateDecimalFromString(string)` static methods

**Core changes:**
- `JSMath.cs`: 6 `is JSDecimal` type checks replaced with `IsDecimal`/`DecimalValue`
- `JSBigIntBuilder.cs`: `JSDecimalBuilder.New()` changed to call
  `JSValue.CreateDecimalFromString()` via `StaticCallExpression`
- Removed `using Broiler.JavaScript.Core.Core.Decimal` from JSMath and JSBigIntBuilder

**Moved:** `JSDecimal.cs` (including `JSDecimalExtensions`) to `BuiltIns/Decimal/`

#### 4. JSIntl Family (Core → BuiltIns)

**Factory delegates added:**
- `JSGlobalStatic.IntlFactory`: `Func<JSValue>` replacing direct `typeof(JSIntl)` reference
- `JSDate.IntlDateFormatter`: `Func<CultureInfo, DateTimeOffset, JSObject, JSValue>`
  replacing direct `JSIntlDateTimeFormat.Get(culture).Format(value, obj)` call

**Moved:** `JSIntl.cs` (containing `JSIntl`, `JSIntlDateTimeFormat`,
`JSIntlRelativeTimeFormat`) to `BuiltIns/Intl/`

#### 5. BuiltIns Module Initializer Wiring

All factory delegates wired in `BuiltInsAssemblyInitializer.Initialize()`:

```csharp
IJSDisposableStack.CreateNew = static () => new JSDisposableStack();
JSGlobalStatic.IntlFactory = static () => JSContext.ClrInterop.GetClrType(typeof(JSIntl));
JSDate.IntlDateFormatter = static (culture, value, options) =>
    JSIntlDateTimeFormat.Get(culture).Format(value, options);
JSValue.CreateDecimalFactory = static v => new JSDecimal(v);
JSValue.CreateDecimalFromStringFactory = static s => new JSDecimal(s);
```

#### 6. Tests Added

13 new tests in `BuiltIns.Tests`:

| Test Class | Tests | Coverage |
|-----------|-------|----------|
| `SuppressedErrorTests` | 3 | Registration, construction, error/suppressed properties |
| `DisposableStackFactoryTests` | 2 | Factory delegate wiring, instance creation |
| `JSDecimalTests` | 6 | Registration, construction, typeof, factory delegates, Math.floor |
| `JSIntlTests` | 2 | Global availability |

### Remaining Candidates Assessment

The following types were audited for extraction feasibility:

| Type | `is` checks in Core/Compiler | Assessment |
|------|------------------------------|------------|
| **JSArray** | 12 | ❌ Impractical — deeply coupled in spread/destructuring, iterator, comparison, structured clone, and array literal compilation |
| **JSString** | 16 | ❌ Impractical — used in arithmetic coercion, equality, template literal handling, and type-of checks throughout Core |
| **JSNumber** | 15 | ❌ Impractical — fundamental to all arithmetic, comparison, and coercion logic |
| **JSError** | 6 | ❌ Impractical — exception handling requires Core to construct errors directly |
| **JSPromise** | 6 | ❌ Impractical — async/await compilation and runtime depend on concrete promise type |
| **JSRegExp** | 8 | ❌ Impractical — used in string methods, compiler literal handling, and structured clone |
| **JSProxy** | 0 | ❌ Not beneficial — zero type checks but referenced in source-generated `Names.g.cs` |
| **JSJSON** | 0 | ❌ Not beneficial — same as JSProxy |
| **JSDataView** | 0 | ❌ Not beneficial — same as JSProxy |

**Conclusion:** All practical extraction candidates have been completed. The
remaining types (JSArray, JSString, JSNumber, JSError, JSPromise, JSRegExp)
have 12–16 type checks each deeply embedded in Core's arithmetic, comparison,
and compilation logic. Extracting them would require pervasive virtual method
overrides in hot paths, risking measurable performance regressions. Types with
zero type checks (JSProxy, JSJSON, JSDataView) are still coupled via the source
generator's `Names.g.cs` registration code, meaning extraction would require
changes to the `JSClassGenerator` source generator itself — disproportionate
effort for negligible architectural benefit.

### Architecture Impact

The BuiltIns assembly now contains **12 source files** across 5 subdirectories:

```
BuiltIns/
├── Decimal/JSDecimal.cs
├── Disposable/JSDisposableStack.cs
├── Disposable/JSSuppressedError.cs
├── Events/CustomEvent.cs
├── Events/DomEventHandler.cs
├── Events/Event.cs
├── Events/EventTarget.cs
├── Intl/JSIntl.cs
├── Weak/WeakRef.cs
├── BuiltInsAssemblyInitializer.cs
├── GlobalUsings.cs
└── Names.cs
```

**Dependency direction:** Core → Runtime (interfaces) ← BuiltIns (implementations).
No reverse dependencies (Core/Compiler → BuiltIns) exist. The Compiler references
only `IJSDisposableStack` from Runtime.

**New contract interfaces/delegates added to Runtime/Core:**
- `IJSDisposableStack` (Runtime)
- `JSValue.IsDecimal` / `JSValue.DecimalValue` (Runtime virtual properties)
- `JSValue.CreateDecimalFactory` / `JSValue.CreateDecimalFromStringFactory` (Runtime)
- `JSGlobalStatic.IntlFactory` (Core)
- `JSDate.IntlDateFormatter` (Core)

### Test Results

All **1024** tests pass across 11 test projects:
- Core: 641, Ast: 73, Parser: 78, Storage: 100, Debugger: 23, Clr: 29,
  Compiler: 9, Modules: 9, **BuiltIns: 29**, Runtime: 20, ModuleExtensions: 13.

---

## 32. Refactor Summary and Completion Status (2026-03-21)

This section provides a consolidated summary table of all extraction phases,
their completion status, and what is deferred. It is intended as the primary
reference for maintainers and new contributors to quickly understand the state
of the Broiler.JavaScript assembly refactor.

### Completed vs. Deferred Summary

| Area | Status | Details | Reference |
|------|--------|---------|-----------|
| **Ast extraction** | ✅ Complete | 53 AST node types in `Broiler.JavaScript.Ast`; 73 tests | Phase 1 (Section 9) |
| **Parser extraction** | ✅ Complete | Lexer + parser in `Broiler.JavaScript.Parser`; 78 tests | Phase 2 (Section 9) |
| **Storage extraction** | ✅ Complete | All storage types (`JSProperty`, `PropertySequence`, `ElementArray`, `KeyString`) in `Broiler.JavaScript.Storage`; 100 tests | Phase 3 (Section 9, 17–20) |
| **Debugger extraction** | ✅ Complete | V8 Inspector Protocol in `Broiler.JavaScript.Debugger`; `InternalsVisibleTo` bridge removed; 23 tests | Phase 4 (Section 9) |
| **Clr extraction** | ✅ Complete | .NET ↔ JS bridging in `Broiler.JavaScript.Clr`; `InternalsVisibleTo` bridge removed; 29 tests | Phase 5 (Section 9) |
| **BuiltIns extraction** | ✅ Complete (practical limit) | 12 types extracted via factory delegates (Events, Weak, Disposable, Decimal, Intl); remaining candidates assessed as impractical; 29 tests | Phase 6 (Sections 27, 31) |
| **Compiler extraction** | ✅ Complete | AST → LINQ compilation in `Broiler.JavaScript.Compiler`; `InternalsVisibleTo` bridge removed; 9 tests | Phase 7 (Section 9) |
| **Modules extraction** | ✅ Complete | ES module system in `Broiler.JavaScript.Modules`; 9 tests | Phase 8 (Section 9) |
| **Runtime extraction** | ✅ Substantially complete | Core value types, interface abstractions, contract interfaces, `CoreScript` in `Broiler.JavaScript.Runtime`; 20 tests | Phase 9a–9d (Sections 16–25) |
| **Cleanup and CI** | ✅ Complete | All `InternalsVisibleTo` migration bridges removed; meta-package created; CI pipeline covers 11 test projects on 3 platforms | Phase 10 (Sections 14–15) |
| **ModuleExtensions** | ✅ Complete | Namespace, TFM, and reference alignment; bugs fixed; 13 tests | Section 30 |
| **Concrete type extraction** (JSObject/JSFunction/JSContext) | ⏳ Deferred | 500+ file references, 22 cascade subtypes — deferred per architectural assessment | Section 26 |
| **Remaining BuiltIns** (JSArray, JSString, JSNumber, JSError, JSPromise, JSRegExp) | ❌ Not planned | Deep structural coupling (12–16 type checks each); extraction would risk performance regressions | Sections 27, 31 |
| **Coverage ≥ 90% per assembly** | ⏳ In progress | `coverlet` integrated; thresholds not yet enforced | Section 29.1 |
| **Integration test project** | 📋 Planned | `Broiler.JavaScript.Integration.Tests` not yet created | Section 29.3 |
| **WebAtoms.XF bridge removal** | ⏳ External | Requires third-party coordination | Section 29.6 |

### Assembly Inventory (current)

| Assembly | Source Files | Tests | Depends On |
|----------|-------------|-------|------------|
| `Broiler.JavaScript.Ast` | 53 | 73 | *(none)* |
| `Broiler.JavaScript.Parser` | 35 | 78 | Ast, ExpressionCompiler |
| `Broiler.JavaScript.Storage` | 7+ | 100 | Ast |
| `Broiler.JavaScript.Debugger` | 26 | 23 | Core |
| `Broiler.JavaScript.Clr` | 11 | 29 | Core |
| `Broiler.JavaScript.BuiltIns` | 12 | 29 | Core, Runtime |
| `Broiler.JavaScript.Compiler` | 40+ | 9 | Ast, Runtime, ExpressionCompiler |
| `Broiler.JavaScript.Modules` | 4 | 9 | Core |
| `Broiler.JavaScript.Runtime` | 24 | 20 | Ast, Storage, ExpressionCompiler |
| `Broiler.JavaScript.ModuleExtensions` | 2 | 13 | Core, Modules |
| `Broiler.JavaScript.Core` | 615+ (reduced) | 641 | Runtime, Storage, Ast, ExpressionCompiler |
| **Total** | — | **1024** | — |

### Implementation Log Cross-Reference

All major extraction phases are documented in the Implementation Log (Section 9)
with dates, rationales, and specific file-level changes. The following table
maps each phase to its primary documentation sections:

| Phase | Date | Primary Section(s) | Key Decisions |
|-------|------|-------------------|---------------|
| 1 — Ast | 2026-03-19 | Section 9 (Phase 1) | Data-only AST nodes; zero runtime references |
| 2 — Parser | 2026-03-19 | Section 9 (Phase 2) | Lexer + parser; depends only on Ast |
| 3 — Storage | 2026-03-21 | Section 9 (Phase 3), Sections 17–20 | Interface-typed fields resolved circular deps; `PropertyValueEnumerator` remains in Core |
| 4 — Debugger | 2026-03-19 | Section 9 (Phase 4) | `IDebugger` contract interface; public API only |
| 5 — Clr | 2026-03-20 | Section 9 (Phase 5) | `IClrInterop` contract; bridge removed |
| 6 — BuiltIns | 2026-03-20–21 | Sections 27, 31 | Factory delegates for decoupling; 12 types extracted; remaining assessed as impractical |
| 7 — Compiler | 2026-03-20 | Section 9 (Phase 7) | 40+ partial files; bridge removed |
| 8 — Modules | 2026-03-20 | Section 9 (Phase 8) | ES module system; depends on Core |
| 9a — Storage types | 2026-03-21 | Sections 17–20 | `JSProperty`, `PropertySequence`, `ElementArray`, `KeyString` moved to Storage |
| 9b — Value types | 2026-03-21 | Sections 21–22 | `JSValue`, `Arguments`, `PropertyKey`; `IJSPrototype`/`IJSSymbol` abstractions |
| 9c — Contract interfaces | 2026-03-21 | Sections 23–24 | `IDebugger`, `IClrInterop`, `IJSCompiler`, `ICodeCache`, `IBuiltInRegistry` moved to Runtime |
| 9d — CoreScript | 2026-03-21 | Section 25 | Factory delegate pattern; `IJSFunction` interface; `IJSContext` enriched |
| 10 — Cleanup | 2026-03-20 | Sections 14–15 | All migration bridges removed; meta-package; downstream updates |
| ModuleExtensions | 2026-03-21 | Section 30 | Namespace aligned; bugs fixed; 13-test project |
| BuiltIns completion | 2026-03-21 | Section 31 | JSDisposableStack, JSDecimal, JSIntl via factory delegates |

### CI/CD and Test Coverage Summary

**CI configuration:** `.github/workflows/ci.yml`
- **Trigger:** Push to `main`, pull requests to `main`
- **Matrix:** `ubuntu-latest`, `windows-latest`, `macos-latest`
- **Steps:** Checkout → Setup .NET 8.0 → Restore → Build → Test (11 projects)
- **Coverage:** `coverlet.collector` v6.0.2 with `--collect:"XPlat Code Coverage"`

**Current test results (2026-03-21):**

| Test Project | Tests | Status |
|-------------|-------|--------|
| Core.Tests | 641 | ✅ Pass |
| Storage.Tests | 100 | ✅ Pass |
| Parser.Tests | 78 | ✅ Pass |
| Ast.Tests | 73 | ✅ Pass |
| Clr.Tests | 29 | ✅ Pass |
| BuiltIns.Tests | 29 | ✅ Pass |
| Debugger.Tests | 23 | ✅ Pass |
| Runtime.Tests | 20 | ✅ Pass |
| ModuleExtensions.Tests | 13 | ✅ Pass |
| Compiler.Tests | 9 | ✅ Pass |
| Modules.Tests | 9 | ✅ Pass |
| **Total** | **1024** | **✅ All pass** |

**Coverage status:** Data collection integrated into CI. Per-assembly coverage
thresholds (≥ 90% target) not yet enforced. See Section 29.1 for action plan.

---

## 33. Known Gaps and Deferred Work (2026-03-21)

This section enumerates all known gaps and intentionally deferred items from the
assembly refactor, with rationales and cross-references. Each item is classified
by priority and current status.

### 33.1 BuiltIns Extraction — Remaining Types in Core

**Types:** `JSArray`, `JSString`, `JSNumber`, `JSError`, `JSPromise`, `JSRegExp`,
`JSProxy`, `JSJSON`, `JSDataView`

**Rationale:** These types have 6–16 direct type checks (`is` patterns) deeply
embedded in Core's arithmetic, comparison, coercion, and compilation logic.
Extracting them would require pervasive virtual method overrides in hot paths,
risking measurable performance regressions. Types with zero type checks
(`JSProxy`, `JSJSON`, `JSDataView`) are coupled via the `JSClassGenerator`
source generator's `Names.g.cs` registration code. See Section 31 for the full
candidate assessment.

**Status:** ❌ Not planned — assessed as impractical (2026-03-21).

### 33.2 Concrete Runtime Type Extraction (JSObject / JSFunction / JSContext)

**Types:** `JSObject`, `JSFunction`, `JSContext`, `JSPrototype`, `JSSymbol`,
and 22+ subtypes inheriting from `JSObject`.

**Rationale:** These types are referenced in 500+ source files across Core and
satellite assemblies. `JSObject` alone has 22 subtype cascades. Extraction would
require an `ObjectModel` intermediate assembly and extensive internal API
surfacing, with high regression risk. The current interface-based architecture
(`IJSContext`, `IJSFunction`, `IJSPrototype`, `IJSSymbol`) satisfies all known
downstream requirements. See Section 26 for the full architectural assessment.

**Status:** ⏳ Deferred — revisit only if a concrete downstream need arises.

**Preconditions for future extraction (Section 29.5):**
- P1 coverage targets achieved (≥ 90% per assembly)
- P2 integration test project created
- Concrete downstream demand identified

### 33.3 Test Coverage Thresholds

**Goal:** ≥ 90% line coverage per extracted assembly (Success Criterion #2).

**Current state:** `coverlet.collector` is integrated into all 11 test projects
and CI collects Cobertura XML reports. However, per-assembly coverage reports
have not been reviewed, and CI does not enforce minimum thresholds.

**Status:** ⏳ In progress — see Section 29.1 for detailed action plan.

### 33.4 Integration Test Project

**Goal:** Create `Broiler.JavaScript.Integration.Tests` to validate cross-assembly
behavior (module initializer ordering, factory delegate wiring,
`TypeForwardedTo` backward compatibility).

**Status:** 📋 Planned — Section 29.3 contains the full action plan. Currently,
integration is validated through `Core.Tests` (641 tests) which exercises
cross-assembly boundaries indirectly.

### 33.5 WebAtoms.XF InternalsVisibleTo Bridge

**Current state:** `WebAtoms.XF` is an external consumer retaining one
`InternalsVisibleTo` entry in `Core/AssemblyInfo.cs`. All migration bridges
have been removed; this is the only remaining external bridge.

**Status:** ⏳ External dependency — requires coordination with `WebAtoms.XF`
maintainers. See Section 29.6 for the communication and deprecation plan.

### 33.6 Downstream Consumer Migration Documentation

**Current state:** Section 11 documents migration steps for downstream consumers.
`Broiler.App` and `Broiler.Cli` have been updated. Documentation has not been
fully verified end-to-end after Phase 9d changes.

**Status:** ⏳ Needs verification — see Section 29.4 for action plan.

### 33.7 Per-Type BuiltIns Assemblies

**Description:** Breaking the `BuiltIns` assembly into per-type assemblies
(e.g., `BuiltIns.Promise`, `BuiltIns.Intl`) for finer-grained dependency
control.

**Status:** ❌ Not planned — only revisit if consumers need to reference
individual built-in types independently. See Section 29.7.

### 33.8 Primitives Assembly

**Description:** A potential `Primitives` assembly was discussed (Sections 3.6,
6.1) to break circular dependencies if they appeared between Runtime and Storage.

**Status:** ❌ Not needed — no circular dependencies exist. The current
dependency direction (Runtime → Storage → Ast) is clean and unidirectional.

### 33.9 Legacy Namespace and Migration Aid Removal

**Description:** 31 `TypeForwardedTo` attributes exist in `Core/AssemblyInfo.cs`
for binary backward compatibility. These ensure that code referencing types via
their original `Core` namespace continues to work transparently.

**Status:** ✅ Functioning as intended — these are permanent compatibility shims,
not temporary migration aids. They should remain as long as downstream consumers
reference types via the `Core` assembly. No `global using` migration aids are
in use.

---

## 34. Done Definition and Completion Criteria (2026-03-21)

This section clarifies what constitutes "done" for the Broiler.JavaScript
assembly refactor and what is explicitly out of scope.

### What "Done" Means for This Refactor

The immediate refactor is considered **substantially complete** when all of the
following criteria are met:

| # | Criterion | Status | Notes |
|---|-----------|--------|-------|
| 1 | Core decomposed: major subsystems extracted into dedicated assemblies | ✅ Met | 8 of 11 target assemblies extracted; concrete types intentionally remain in Core (Section 26) |
| 2 | Test coverage: dedicated test projects for each assembly | ✅ Met (structural) | 11 test projects created with 1024 tests; coverage thresholds (≥ 90%) not yet enforced |
| 3 | Existing tests pass: no regressions from extraction | ✅ Met | All 1024 tests pass (verified 2026-03-21) |
| 4 | Downstream consumers build correctly | ✅ Met | `Broiler.App`, `Broiler.Cli` updated; `Broiler.JavaScript.All` meta-package available |
| 5 | No `InternalsVisibleTo` migration bridges | ✅ Met | All migration bridges eliminated; only test, runtime dynamic assembly, and external (`WebAtoms.XF`) entries remain |
| 6 | CI pipeline covers all assemblies | ✅ Met | 11 test projects on 3 platforms with coverage collection |
| 7 | No circular assembly dependencies | ✅ Met | Verified — unidirectional dependency graph |
| 8 | Downstream build instructions updated | ✅ Met | Section 11 documents migration steps |

### What Is Explicitly Out of Scope

The following items are **not** considered blockers for the refactor's completion.
They are documented as future work (Section 29) and known gaps (Section 33):

1. **Concrete type extraction** (`JSObject`, `JSFunction`, `JSContext`) —
   deferred per architectural assessment (Section 26). The interface-based
   abstraction layer satisfies all known requirements.

2. **Full BuiltIns extraction** — remaining types (JSArray, JSString, JSNumber,
   JSError, JSPromise, JSRegExp) assessed as impractical to extract due to deep
   structural coupling (Section 31).

3. **Coverage threshold enforcement** — `coverlet` integration is complete but
   ≥ 90% line coverage thresholds are not yet enforced in CI (Section 29.1).

4. **Integration test project** — cross-assembly integration is currently
   validated via `Core.Tests` (Section 29.3).

5. **WebAtoms.XF bridge removal** — external coordination required (Section 29.6).

### When to Revisit

- **Concrete type extraction:** Only if a downstream consumer requires depending
  on `JSObject` without pulling in `Core`. No such requirement exists today.
- **BuiltIns extraction:** Only if performance profiling shows that the factory
  delegate pattern does not introduce measurable overhead, and if there is
  consumer demand for per-type BuiltIn references.
- **Coverage thresholds:** As part of the next quality milestone (Section 29.1).
- **Integration tests:** Before any future large-scale extraction (Section 29.5
  lists this as a precondition for ObjectModel extraction).
