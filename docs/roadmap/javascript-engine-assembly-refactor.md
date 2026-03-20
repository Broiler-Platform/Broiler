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
| `Broiler.JavaScript.Storage` | Broiler.JavaScript.Storage | Property hash maps, virtual memory, concurrent caches | ✅ Extracted (Phase 3, partial) |
| `Broiler.JavaScript.Debugger` | Broiler.JavaScript.Debugger | V8 Inspector Protocol handler, protocol data types | ✅ Extracted (Phase 4, partial); `InternalsVisibleTo` bridge removed |
| `Broiler.JavaScript.Clr` | Broiler.JavaScript.Clr | .NET ↔ JavaScript type bridging (`ClrProxy`, `ClrType`, `DefaultClrInterop`) | ✅ Extracted (Phase 5) |
| `Broiler.JavaScript.ExpressionCompiler` | Broiler.JavaScript.ExpressionCompiler | LINQ Expression Tree → IL compilation | Pre-existing |
| `Broiler.JavaScript.JSClassGenerator` | Broiler.JavaScript.JSClassGenerator | Roslyn source generator for C#-to-JS bindings | Pre-existing |
| `Broiler.JavaScript.Network` | YantraJS.Network | Fetch API / network module | Pre-existing |
| `Broiler.JavaScript.ModuleExtensions` | (library) | Fluent module-registration extensions | Pre-existing |
| `Broiler.JavaScript.NodePollyfill` | YantraJS.NodePollyfill | Node.js compatibility polyfills | Pre-existing |
| `Broiler.JavaScript` | YantraJS (exe) | CLI REPL / runner | Pre-existing |
| `Broiler.JavaScript.Core.Tests` | (test) | Unit tests for the core engine (641 tests) | Active |
| `Broiler.JavaScript.Ast.Tests` | (test) | Unit tests for Ast assembly (73 tests) | ✅ Created |
| `Broiler.JavaScript.Parser.Tests` | (test) | Unit tests for Parser assembly (78 tests) | ✅ Created |
| `Broiler.JavaScript.Storage.Tests` | (test) | Unit tests for Storage assembly (56 tests) | ✅ Created |
| `Broiler.JavaScript.Debugger.Tests` | (test) | Unit tests for Debugger assembly (23 tests) | ✅ Created |
| `Broiler.JavaScript.Clr.Tests` | (test) | Unit tests for Clr assembly (29 tests) | ✅ Created |

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

**Purpose:** Core execution environment — the types every other assembly needs
to interact with JavaScript values and contexts.

**Public API:**
- `JSContext` — execution context (create, evaluate, dispose)
- `JSValue` — base type for all JavaScript values
- `JSObject`, `JSFunction`, `JSPrototype`
- `Arguments` — function invocation arguments
- `CoreScript` — high-level compile-and-evaluate bridge
- `KeyString`, `KeyStrings` — interned property names
- `JSException` — JavaScript exception wrapper
- `IBuiltInRegistry` — registration interface for built-in types
- `IClrInterop` — contract for CLR interop (consumed, not implemented here)
- `IDebugger` — contract for debugger integration

**Design Rules:**
- Depends on **Ast** (for `CoreScript` which calls the compiler) and
  **Storage**.
- `Bootstrap` must become pluggable: instead of hard-coding every built-in
  type, it calls `IBuiltInRegistry.Register(JSContext)` so that `BuiltIns`,
  `Clr`, and `Modules` assemblies register themselves.
- IL emission helpers (`Emit/`) stay here because they are tightly coupled to
  the runtime's `DynamicMethod` generation.

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
  for this phase.

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
| **Phase 3** | **Storage** | Depends on shared primitives. Decouples property storage from runtime logic. | ✅ Partial — pure storage types extracted; test project created (56 tests) |
| **Phase 4** | **Debugger** | Already behind `IDebugger` interface. Largely independent. | ✅ Partial — V8 Inspector Protocol extracted; test project created (23 tests); `InternalsVisibleTo` bridge removed (all accessed APIs now public) |
| **Phase 5** | **Clr** | Already behind `IClrInterop` interface. Medium coupling. | ✅ Complete — 11 files extracted; ClrProxyBuilder decoupled via delegate pattern; FallbackClrInterop as Core default; test project created (29 tests) |
| **Phase 6** | **BuiltIns** | High coupling to Runtime, but only through `JSValue`/`JSContext`. Requires `IBuiltInRegistry` to be in place. | ⏳ In progress — IBuiltInRegistry implemented and wired into JSContext; JSClassGenerator updated to emit JSContext.ClrInterop.Marshal(); multi-assembly namespace support still needed |
| **Phase 7** | **Compiler** | Depends on Ast, Runtime, and ExpressionCompiler. Requires stable interfaces. | ⏳ Blocked — requires stable Runtime interfaces |
| **Phase 8** | **Modules** | Last — depends on Runtime, Parser, and Clr. | ⏳ Blocked — JSFunctionGenerator creates partial class in Core namespace; JSModuleContext extends JSContext |

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

**Phase 3 — Storage: ✅ Partial (2026-03-19)**

**Phase 4 — Debugger: ✅ Partial (2026-03-19)**

**Deliverables:**
- `Broiler.JavaScript.Storage` assembly ✅ (pure storage types)
- `Broiler.JavaScript.Debugger` assembly ✅ (V8 Inspector Protocol)
- `Broiler.JavaScript.Storage.Tests` — 56 assembly-specific tests ✅ (2026-03-20)
- `Broiler.JavaScript.Debugger.Tests` — 23 assembly-specific tests ✅ (2026-03-20)

**Key Metrics:**
- Storage has no reference to `JSContext` (only to Ast shared primitives). ✅
- Debugger accesses Core via public APIs only (no `InternalsVisibleTo` needed). ✅
- Storage.Tests references only Storage (no Core dependency). ✅
- 3 runtime-dependent storage types (JSProperty, PropertySequence, ElementArray)
  remain in Core until Runtime extraction resolves the circular dependency.

### Milestone 3 — Interop Extraction (Phases 5–6)

**Status:** Phase 5 complete; Phase 6 blocked

**Deliverables:**
- `Broiler.JavaScript.Clr` assembly ✅
- `Broiler.JavaScript.Clr.Tests` — 29 assembly-specific tests ✅
- `Broiler.JavaScript.BuiltIns` assembly — not yet started
- `IBuiltInRegistry` pluggable bootstrap in Runtime ✅

**Prerequisites (see Phase 5–8 analysis in Implementation Log):**
1. ~~Refactor Core to use `IClrInterop` exclusively~~ — ✅ Done.
2. ~~Implement `IBuiltInRegistry` pluggable bootstrap in Core~~ — ✅ Done.
3. Configure `JSClassGenerator` to work with extracted assemblies — still needed
   for Phase 6.

**Key Metrics:**
- `Broiler.JavaScript.Clr` compiles independently with zero errors. ✅
- `JSContext` bootstrap is driven entirely by `IBuiltInRegistry`. ✅
- CLR interop is pluggable via `IClrInterop` interface. ✅
- Removing the Clr assembly from the dependency chain produces a functional
  (but feature-reduced) runtime using `FallbackClrInterop`. ✅

### Milestone 4 — Compiler and Modules (Phases 7–8)

**Status:** Blocked — prerequisites not yet met

**Deliverables:**
- `Broiler.JavaScript.Compiler` assembly
- `Broiler.JavaScript.Modules` assembly
- Full integration test suite verifying end-to-end script execution

**Prerequisites (see Phase 5–8 analysis in Implementation Log):**
1. Define stable Runtime interfaces for compiler consumption.
2. Resolve `JSModuleContext extends JSContext` inheritance dependency.
3. Configure `JSClassGenerator` for multi-assembly namespace support.

**Key Metrics:**
- All 10 assemblies compile independently.
- Existing `Broiler.JavaScript.Core.Tests` pass (may be split across
  assembly-specific test projects).
- `Broiler.App` and `Broiler.Cli` build and function correctly.

---

## 6. Implementation Guidance

### 6.1 Cross-Assembly Communication

**Interfaces over concrete types.** Assemblies that consume services from other
assemblies should depend on interfaces, not concrete classes. Key interfaces:

| Interface | Defined In | Implemented In |
|-----------|-----------|----------------|
| `IBuiltInRegistry` | Runtime | BuiltIns, Clr |
| `IClrInterop` | Runtime | Clr |
| `IDebugger` | Runtime | Debugger |
| `IJSModuleResolver` | Runtime | Modules |

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
| Compiler | `Broiler.JavaScript.Compiler.Tests` | Expression tree generation, generator rewriting | Future |
| Runtime | `Broiler.JavaScript.Runtime.Tests` | `JSContext` lifecycle, `JSValue` coercion, `Arguments` | Future |
| Storage | `Broiler.JavaScript.Storage.Tests` | Property map operations, hash collision handling | ✅ 56 tests |
| BuiltIns | `Broiler.JavaScript.BuiltIns.Tests` | Per-object spec-conformance (Array, String, Date, Promise, etc.) | Future |
| Clr | `Broiler.JavaScript.Clr.Tests` | ClrProxy marshalling, ClrType caching, DefaultClrInterop, expression builder registration | ✅ 29 tests |
| Modules | `Broiler.JavaScript.Modules.Tests` | Import/export resolution, circular dependencies | Future |
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

| Risk | Mitigation |
|------|------------|
| **Breaking downstream builds** | Use `TypeForwardedTo` attributes and `global using` aliases during migration to preserve API compatibility. |
| **Circular dependencies** | Extract shared primitives (`StringSpan`, `KeyString` base) into **Ast** assembly. Use interfaces for upward references. |
| **Performance regression** | Assembly boundaries add no runtime cost (same AppDomain, same JIT). Benchmark critical paths (parse → compile → execute) before and after each phase. |
| **Test coverage gaps** | Require each phase to achieve ≥ 90% line coverage on the extracted assembly before merging. |
| **Scope creep** | Each phase is a self-contained PR. No functional changes — only structural moves and interface introductions. |

---

## 8. Success Criteria

The refactor is complete when:

1. `Broiler.JavaScript.Core` no longer exists as a single monolithic assembly.
   Its code is distributed across the assemblies defined in Section 2.
2. Each assembly has a dedicated test project with comprehensive coverage.
3. All existing tests in `Broiler.JavaScript.Core.Tests` pass (distributed
   across the new test projects).
4. `Broiler.App`, `Broiler.Cli`, and `Broiler.Avalonia` build and run correctly
   with the new assembly structure.
5. No `InternalsVisibleTo` entries remain as migration bridges.
6. The CI pipeline (`.github/workflows/ci.yml`) builds and tests all new
   assemblies on Linux, macOS, and Windows.

---

## 9. Implementation Log

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

**Phase 6 (BuiltIns) — partially unblocked:**
- `IBuiltInRegistry` is fully implemented with `DefaultBuiltInRegistry`.
- `JSContext` constructor uses the pluggable registry pattern.
- JSClassGenerator now emits `JSContext.ClrInterop.Marshal()` — ✅ done.
- **Remaining blocker:** JSClassGenerator must support configurable namespace
  roots / multi-assembly code generation.

**Phase 7 (Compiler) — unchanged:**
- `FastCompiler` references runtime types extensively in expression tree
  generation. These references are inherent to the compilation model.
- Extraction requires stable Runtime interfaces.

**Phase 8 (Modules) — unchanged:**
- `JSModule` extends `JSObject` and uses `[JSFunctionGenerator]`.
- `JSModuleContext` extends `JSContext`.
- Module files now use `ClrInterop.Marshal()` instead of `ClrProxy.Marshal()`,
  but the `ClrModule.Default` reference and namespace-linked generated partial
  class issues remain.

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
7. Configure `JSClassGenerator` multi-assembly namespace support (prerequisite for
   Phase 6).
8. Define stable Runtime interfaces for compiler consumption (prerequisite for
   Phase 7).

**Estimated effort for remaining prerequisites:**
- Remaining structural `ClrProxyBuilder` references: Low — 2 call sites in
  expression tree builder; tightly coupled to compilation model. Will be
  resolved naturally during Phase 7 (Compiler extraction).
- `JSClassGenerator` multi-assembly: Medium — must support configurable namespace
  roots or assembly-aware code generation.
- Runtime interfaces: High — must define stable abstractions for `JSValue`,
  `JSContext`, `Arguments`, and `JSFunction` that the Compiler can reference
  without pulling in the full Runtime implementation.

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

*This roadmap tracks the creation and documentation of the refactor plan, not
the refactor itself. Implementation issues should be created per milestone
and linked back to this document.*
