# Broiler.JavaScript — Assembly Refactor Roadmap

> **Goal:** A clear, maintainable structure for Broiler.JavaScript, with all major
> logical components in their own assemblies.

---

## Table of Contents

1. [Executive Summary](#1-executive-summary)
2. [Current Assembly Inventory](#2-current-assembly-inventory)
3. [Dependency Graph](#3-dependency-graph)
4. [Decoupling Patterns in Use](#4-decoupling-patterns-in-use)
5. [Audit — What Is Already Separated](#5-audit--what-is-already-separated)
6. [Audit — What Remains Coupled in Core](#6-audit--what-remains-coupled-in-core)
7. [Extraction Candidates](#7-extraction-candidates)
8. [Types Assessed as Impractical to Extract](#8-types-assessed-as-impractical-to-extract)
9. [Outstanding Tasks](#9-outstanding-tasks)
10. [Actionable Steps](#10-actionable-steps)
11. [Milestones & Timeline](#11-milestones--timeline)
12. [Risks & Mitigations](#12-risks--mitigations)
13. [Appendix — File Counts by Assembly](#13-appendix--file-counts-by-assembly)

---

## 1. Executive Summary

The Broiler.JavaScript (JavaScript-Engine) module has been substantially refactored
from a monolithic architecture into **17 distinct assemblies**. The separation follows
a layered architecture with clear dependency direction:

```
ExpressionCompiler ← Ast ← Storage
                     Ast ← Parser
                     Ast, ExpressionCompiler, Storage ← Runtime
                     All of the above ← Core
                     Core ← Compiler, Clr, Debugger, BuiltIns, Modules, Network, NodePollyfill
```

**Key decoupling mechanisms** already in place:
- `[TypeForwardedTo]` attributes (63 total) for binary backward compatibility
- `[ModuleInitializer]` + factory delegates for lazy satellite assembly loading
- `JSClassGenerator` source generator for per-assembly type registration
- `IBuiltInRegistry` / `IClrInterop` / `IJSCompiler` interfaces in `Runtime`

**Remaining work** centers on:
1. ~~Extracting additional built-in types from Core → BuiltIns~~ — 24 types extracted (M2/M3); 1 remaining extractable candidate (JSBigInt, deferred)
2. ~~Evaluating LinqExpressions for extraction into the Compiler assembly~~ — Deferred (tightly coupled by design)
3. ~~Establishing CI, tests, and documentation for the separated structure~~ — CI and 116 tests in place (M1)
4. Aligning target frameworks across all library projects (M5)
5. Final validation and performance benchmarks (M6)

---

## 2. Current Assembly Inventory

| # | Assembly | Purpose | TFM | Source Files |
|---|----------|---------|-----|-------------|
| 1 | `Broiler.JavaScript.ExpressionCompiler` | Expression compilation utilities, IL emit helpers | net8.0 | ~15 |
| 2 | `Broiler.JavaScript.JSClassGenerator` | Roslyn source generator for JS class/function bindings | net8.0 | ~8 |
| 3 | `Broiler.JavaScript.Ast` | AST node definitions (`AstNode`, `FastToken`, `StringSpan`, etc.) | net8.0 | ~35 |
| 4 | `Broiler.JavaScript.Storage` | Property storage (`PropertySequence`, `ElementArray`, `VirtualMemory`, etc.) | net8.0 | ~20 |
| 5 | `Broiler.JavaScript.Parser` | ECMAScript parser (`FastParser`, `FastScanner`, etc.) | net8.0 | ~25 |
| 6 | `Broiler.JavaScript.Runtime` | Runtime interfaces and value-type abstractions | net8.0 | 24 (some files define multiple forwarded types) |
| 7 | `Broiler.JavaScript.Core` | Main JS engine: `JSContext`, `JSObject`, `JSFunction`, built-in types | net8.0 | **~190** |
| 8 | `Broiler.JavaScript.Compiler` | FastCompiler IL code generation | net8.0 | ~30 |
| 9 | `Broiler.JavaScript.Clr` | .NET CLR interop (`ClrProxy`, `ClrType`, marshalling) | net8.0 | ~25 |
| 10 | `Broiler.JavaScript.Debugger` | V8 Chrome DevTools Protocol debugger | net8.0 | ~15 |
| 11 | `Broiler.JavaScript.BuiltIns` | Extended built-in types (ES2024+): events, intl, decimal, disposable | net8.0 | 12 |
| 12 | `Broiler.JavaScript.Modules` | Module system (CommonJS/ESM import/export) | net8.0 | ~10 |
| 13 | `Broiler.JavaScript.ModuleExtensions` | Module builder extensions | net8.0 | ~5 |
| 14 | `Broiler.JavaScript.Network` | Fetch API, Blob, FormData, URL, AbortController | net8.0 | 12 |
| 15 | `Broiler.JavaScript.NodePollyfill` | Node.js polyfills (EventEmitter) | net8.0 | 1 |
| 16 | `Broiler.JavaScript.All` | Meta-package aggregating all engine assemblies | net8.0 | 0 (facade) |
| 17 | `Broiler.JavaScript` | Main executable / CLI entry point | **net9.0** | ~5 |

---

## 3. Dependency Graph

```
                    ┌────────────────────┐
                    │ExpressionCompiler  │ (leaf — no dependencies)
                    └───────┬──┬─────────┘
                            │  │
               ┌────────────┘  └─────────────┐
               ▼                              ▼
        ┌──────────┐                   ┌──────────┐
        │   Ast    │                   │ Storage  │───── Ast
        └────┬─────┘                   └────┬─────┘
             │                              │
        ┌────▼─────┐                        │
        │  Parser  │── Ast, ExprComp        │
        └────┬─────┘                        │
             │                              │
        ┌────▼──────────────────────────────▼──┐
        │              Runtime                  │── Ast, ExprComp, Storage
        └──────────────────┬───────────────────┘
                           │
        ┌──────────────────▼───────────────────┐
        │              Core                     │── Ast, Parser, Storage,
        │  (190 files — main engine)            │   Runtime, ExprComp,
        │                                       │   JSClassGenerator (analyzer)
        └──────┬──────┬──────┬──────┬──────────┘
               │      │      │      │
     ┌─────────▼┐ ┌───▼──┐ ┌▼────┐ ┌▼────────┐
     │ Compiler │ │ Clr  │ │Debug│ │BuiltIns │
     └──────────┘ └──┬───┘ └─────┘ └────┬────┘
                     │                   │
              ┌──────▼──┐         ┌──────▼───┐
              │ Modules │         │ Network  │── BuiltIns, Clr, Core
              └────┬────┘         └──────────┘
                   │
          ┌────────▼──────────┐
          │ ModuleExtensions  │── Clr, Core, ExprComp, Modules
          └───────────────────┘

        ┌──────────────┐
        │NodePollyfill │── Core
        └──────────────┘

  Meta-packages / Facades:
        ┌──────────────────┐
        │Broiler.JS.All    │── Core, Clr, Compiler, Modules, BuiltIns, Debugger
        └──────────────────┘
        ┌──────────────────┐
        │Broiler.JavaScript│── Clr, Core, ExprComp, Modules, Network
        └──────────────────┘
```

---

## 4. Decoupling Patterns in Use

### 4.1 TypeForwardedTo (Binary Compatibility)

When types are moved from Core to a new assembly, `[TypeForwardedTo]` attributes
are placed in Core so that existing consumers referencing the old location continue
to compile and load correctly.

| Forwarding File | Target Assembly | Types Forwarded |
|-----------------|-----------------|-----------------|
| `AstTypeForwarding.cs` | `Broiler.JavaScript.Ast` | 18 types (AstNode, FastToken, StringSpan, etc.) |
| `ParserTypeForwarding.cs` | `Broiler.JavaScript.Parser` | 12 types (FastParser, FastScanner, etc.) |
| `StorageTypeForwarding.cs` | `Broiler.JavaScript.Storage` | 10 types (VirtualMemory, StringMap, etc.) |
| `AssemblyInfo.cs` | `Broiler.JavaScript.Runtime` | 26 types (JSValue, Arguments, PropertyKey, etc.) |
| **Total** | | **~63 type forwards** |

### 4.2 ModuleInitializer + Factory Delegates

Satellite assemblies register functionality into Core via `[ModuleInitializer]` and
static factory delegates, avoiding direct assembly references from Core:

| Assembly | Initializer | What It Wires |
|----------|-------------|---------------|
| `Core` | `CoreScriptCoreExtensions` | Bridges `CoreScript` (Runtime) → `JSContext`, `DefaultJSCompiler`, `AsyncPump` |
| `Compiler` | `CompilerAssemblyInitializer` | Registers `FastCompiler`-based compilation pipeline into `DefaultJSCompiler` |
| `BuiltIns` | `BuiltInsAssemblyInitializer` | Registers additional built-ins, wires `JSDisposableStack`, `JSIntl`, `JSDecimal` factories |
| `Clr` | `ClrAssemblyInitializer` | Registers `DefaultClrInterop`, expression builder, CLR module provider |

### 4.3 Interface-Based Abstraction (Runtime Assembly)

The `Runtime` assembly defines interfaces that Core and satellite assemblies implement:

| Interface | Defined In | Implemented In |
|-----------|-----------|----------------|
| `IJSContext` | Runtime | Core (`JSContext`) |
| `IJSFunction` | Runtime | Core (`JSFunction`) |
| `IJSCompiler` | Runtime | Core (`DefaultJSCompiler`) |
| `IClrInterop` | Runtime | Core (`FallbackClrInterop`), Clr (`DefaultClrInterop`) |
| `IBuiltInRegistry` | Runtime | Core (`DefaultBuiltInRegistry`) |
| `IDebugger` | Runtime | Debugger |
| `IJSModuleResolver` | Runtime | Modules |
| `ICodeCache` | Runtime | Core (`DictionaryCodeCache`) |
| `IJSDisposableStack` | Runtime | BuiltIns (`JSDisposableStack`) |
| `IJSPrototype` | Runtime | Core (`JSPrototype`) |
| `IJSSymbol` | Runtime | Core (`JSSymbol`) |

### 4.4 Source Generator (JSClassGenerator)

The `JSClassGenerator` Roslyn source generator runs independently per assembly,
generating `Names.g.cs` with `KeyString` constants and `RegisterAll` methods. This
allows each assembly (Core, BuiltIns, Network) to have its own `Names` class and
independent registration without cross-assembly coupling.

---

## 5. Audit — What Is Already Separated

### ✅ Fully Separated Assemblies

| Assembly | Status | Notes |
|----------|--------|-------|
| ExpressionCompiler | ✅ Complete | Leaf assembly, zero project references |
| JSClassGenerator | ✅ Complete | Roslyn analyzer, consumed as `OutputItemType="Analyzer"` |
| Ast | ✅ Complete | 18 types forwarded from Core |
| Storage | ✅ Complete | 10 types forwarded from Core |
| Parser | ✅ Complete | 12 types forwarded from Core |
| Runtime | ✅ Complete | 26 types forwarded from Core; defines all decoupling interfaces |
| Compiler | ✅ Complete | Registers via `[ModuleInitializer]`; lazy-loaded by `DefaultJSCompiler` |
| Clr | ✅ Complete | Registers via `[ModuleInitializer]`; `FallbackClrInterop` in Core as fallback |
| Debugger | ✅ Complete | Standalone; depends only on Core |
| BuiltIns | ✅ Complete (current scope) | 12 types extracted; registers via `[ModuleInitializer]` |
| Modules | ✅ Complete | Module system; depends only on Core |
| ModuleExtensions | ✅ Complete | Module builder extensions |
| Network | ✅ Complete | Fetch API + web types; uses JSClassGenerator |
| NodePollyfill | ✅ Complete | EventEmitter only; depends only on Core |
| Broiler.JavaScript.All | ✅ Complete | Meta-package facade |

### ✅ Types Successfully Extracted to BuiltIns

The following 16 types have been extracted from Core to the BuiltIns assembly:

1. `EventTarget` — DOM event target base
2. `Event` — DOM event
3. `CustomEvent` — DOM custom event
4. `DomEventHandler` — Event handler wrapper
5. `JSWeakRef` — WeakRef (ES2021)
6. `JSFinalizationRegistry` — (implicit in WeakRef module)
7. `JSDisposableStack` — Explicit Resource Management (ES2024)
8. `JSSuppressedError` — SuppressedError for disposable
9. `JSDecimal` — Decimal128 (TC39 Stage 1)
10. `JSIntl` — Internationalization API
11. `JSIntlDateTimeFormat` — Intl.DateTimeFormat
12. `JSIntlRelativeTimeFormat` — (implicit in Intl module)
13. `JSMath` — Math global object **(M2)**
14. `JSReflect` — Reflect global object **(M2)**
15. `JSProxy` — Proxy constructor **(M2)**
16. `JSConsole` — Console object **(M2)**

---

## 6. Audit — What Remains Coupled in Core

The `Core` assembly still contains **~190 source files** organized across several
subdirectories. This section catalogs what remains.

### 6.1 Core/ Subdirectory — Built-In JavaScript Types (108 files)

| Subdirectory | Files | Key Types |
|-------------|-------|-----------|
| `Array/` | 19 | `JSArray`, `JSArrayPrototype`, `JSArrayStatic`, + 15 TypedArray variants |
| `String/` | 5 | `JSString`, `JSStringPrototype`, `JSStringStatic`, `JSTemplateString` |
| `Promise/` | 5 | `JSPromise`, `JSPromisePrototype`, `JSPromiseStatic`, `AsyncPump` |
| `Generator/` | 5 | `JSGenerator`, `JSAsyncFunction`, `JSIterator`, `SafeExitException` |
| `Object/` | 4 | `JSObject`, `JSObjectPrototype`, `JSObjectStatic`, `KeyEnumerator` |
| `Date/` | 4 | `JSDate`, `JSDateMath`, `JSDatePrototype`, `JSDateStatic` |
| `Clr/` | 4 | `ClrMemberNamingConvention`, `FallbackClrInterop`, `JSExportAttribute` (×2) |
| `Number/` | 3 | `JSNumber`, `JSNumberPrototype`, `JSNumberStatic` |
| `Symbol/` | 3 | `JSSymbol`, `JSSymbolPrototype`, `JSSymbolStatic` |
| `Storage/` | 3 | `JSPropertyFactory`, `PropertySequenceCoreExtensions`, `PropertyValueEnumerator` |
| `Primitive/` | 4 | `JSNull`, `JSPrimitive`, `JSPrimitiveObject`, `JSUndefined` |
| `Map/` | 2 | `JSMap`, `JSWeakMap` |
| `Set/` | 2 | `JSSet`, `JSWeakSet` |
| `RegExp/` | 2 | `JSRegExp`, `JSRegExpPrototype` |
| `Json/` | 2 | `JSJSON`, `JSJsonParser` |
| `DataView/` | 2 | `DataView`, `DataViewStatic` |
| `Function/` | 2 | `JSFunction`, `JSClassFunction` |
| `Objects/` | 2 | `JSMath`, `JSReflect` |
| `BigInt/` | 1 | `JSBigInt` |
| `Boolean/` | 1 | `JSBoolean` |
| `Class/` | 1 | `JSClass` |
| `Iterator/` | 1 | `JSIteratorObject` |
| `Proxy/` | 1 | `JSProxy` |
| `Debug/` | 1 | `JSConsole` |
| `Global/` | 1 | `JSGlobal` |
| `Module/` | 3 | `DefaultExportAttribute`, `ExportAttribute`, `IJSModuleResolver` |
| Root | ~10 | `JSContext`, `JSException`, `JSPrototype`, `JSValue`, `Names`, `KeyStrings`, etc. |

### 6.2 LinqExpressions/ — IL Builder Layer (41 files)

| Subdirectory | Files | Purpose |
|-------------|-------|---------|
| Root | 33 | Expression builders for every Core type (`JSArrayBuilder`, `JSStringBuilder`, `JSObjectBuilder`, etc.) |
| `GeneratorsV2/` | 8 | Generator/async function rewriting (`GeneratorRewriter`, `MethodRewriter`, etc.) |

These builders construct LINQ expression trees that the Compiler assembly converts
to IL. Each builder references the concrete Core types it constructs expressions for.

### 6.3 Other Core Subdirectories

| Subdirectory | Files | Purpose |
|-------------|-------|---------|
| `Extensions/` | 10 | Extension methods for various types |
| `Utils/` | 9 | Utility helpers |
| `Emit/` | 2 | `DictionaryCodeCache`, `MethodProvider` |
| `CodeGen/` | 2 | `LoopScope`, `ScriptInfo` |
| `FastParser/Compiler/` | 1 | `DefaultJSCompiler` |
| `LambdaGen/` | 1 | `NewLambdaExpression` |
| `Debugger/` | 1 | Debugger helper |
| `Enumerators/` | 1 | Element enumerator helper |
| `Parser/` | 1 | Parser utility |
| `TypeQuery/` | 1 | Type query utility |
| Root | ~16 | Miscellaneous helpers, type forwarding files, global usings |

---

## 7. Extraction Candidates

### 7.1 Ready to Extract → BuiltIns Assembly

These types have been analyzed and confirmed extractable using the existing
`JSClassGenerator` source generator pattern:

| Type | Files | Complexity | Blocker |
|------|-------|-----------|---------|
| ~~`JSProxy`~~ | ~~1~~ | ~~Low~~ | ~~None~~ — **Extracted (M2)** |
| ~~`JSJSON` + `JSJsonParser`~~ | ~~2~~ | ~~Medium~~ | ~~Needs factory delegate~~ — **Extracted (M3)** |
| ~~`JSDataView` + `DataViewStatic`~~ | ~~2~~ | ~~Low~~ | ~~None~~ — **Extracted (M3)** |
| ~~`JSMath`~~ | ~~1~~ | ~~Low~~ | ~~None~~ — **Extracted (M2)** |
| ~~`JSReflect`~~ | ~~1~~ | ~~Low~~ | ~~None~~ — **Extracted (M2)** |
| ~~`JSConsole`~~ | ~~1~~ | ~~Low~~ | ~~None~~ — **Extracted (M2)** via `ConsoleFactory` delegate |
| ~~`JSMap` / `JSWeakMap`~~ | ~~2~~ | ~~Medium~~ | ~~May need factory delegates~~ — **Extracted (M3)** via `StructuredCloneExtension` delegate |
| ~~`JSSet` / `JSWeakSet`~~ | ~~2~~ | ~~Medium~~ | ~~May need factory delegates~~ — **Extracted (M3)** via `StructuredCloneExtension` delegate |
| `JSBigInt` | 1 | Medium | Referenced by Compiler for literal creation — **Evaluated (M4): Extractable** via factory delegate (see §7.1.1) |
| **Total** | **1 remaining** | | |

### 7.1.1 M4 Evaluation — JSBigInt Extraction Feasibility

**Status:** ✅ Evaluated (M4) — **Extractable** with factory delegate

**Coupling audit results:**

| Coupling Point | File | Lines | Severity |
|----------------|------|-------|----------|
| Compiler literal | `FastCompiler.VisitLiteral.cs` | 25–26 | Medium — uses `JSBigIntBuilder.New()` |
| Compiler unary negate | `FastCompiler.VisitUnaryExpression.cs` | 35–36 | Medium — uses `JSBigIntBuilder.New("-" + ...)` |
| LinqExpression builder | `JSBigIntBuilder.cs` | 10 | **High** — `NewExpression<JSBigInt>()` directly instantiates |
| JSGlobal SetInterval | `JSGlobal.cs` | 130 | Low — `new JSBigInt(key)` for timer IDs |
| JSGlobal SetTimeout | `JSGlobal.cs` | 154 | Low — `new JSBigInt(key)` for timer IDs |
| JSGlobal StructuredClone | `JSGlobal.cs` | 190 | Low — `value is JSBigInt` type check |

**Extraction strategy — Factory delegate (like JSDecimal):**

The existing `JSDecimalBuilder` pattern demonstrates the solution:
- `JSDecimalBuilder.New()` calls `JSValue.CreateDecimalFromString()` — a static
  method backed by a factory delegate, avoiding direct type references.
- `JSBigIntBuilder` can be refactored similarly:
  1. Add `Func<string, JSValue> CreateBigIntFromStringFactory` on `JSValue`
  2. Add `JSValue.CreateBigIntFromString(string)` static method
  3. Change `JSBigIntBuilder.New()` to use `StaticCallExpression` (like `JSDecimalBuilder`)
  4. Wire factory in `BuiltInsAssemblyInitializer`
- JSGlobal timer ID creation (`new JSBigInt(key)`) needs an additional
  `Func<long, JSValue> CreateBigIntFactory` for numeric construction.
- The `value is JSBigInt` check in StructuredClone can use `value.TypeStringValue`
  comparison against `"bigint"` instead.

**Estimated effort:** Small (1–2 hours). Three-step change: factory delegate, builder
refactor, JSGlobal decoupling.

**Recommendation:** Extract in a future milestone if further Core slimming is desired.
Not urgent — JSBigInt is a single file with low maintenance overhead in Core.

### 7.2 Potential New Assembly — TypedArrays

The 15 TypedArray files in `Core/Array/Typed/` represent a cohesive subsystem that
could form its own assembly (`Broiler.JavaScript.TypedArrays`):

- `JSArrayBuffer`, `JSFloat16Array`, `JSFloat32Array`, `JSFloat64Array`
- `JSInt8Array`, `JSInt16Array`, `JSInt32Array`
- `JSUInt8Array`, `JSUInt16Array`, `JSUInt32Array`, `JSUint8ClampedArray`

> **Note:** The casing `JSUint8ClampedArray` (lowercase `i`) matches the source
> file name in the codebase, which differs from the other TypedArray names.
- `JSTypedArray` (base class), `TypedArrayParameters`

**Previous assessment:** `JSTypedArray` extends `JSObject` (Core), and Compiler
generates IL that constructs TypedArray instances. Factory delegates would be needed.

### 7.2.1 M4 Evaluation — TypedArrays Extraction Feasibility

**Status:** ✅ Evaluated (M4) — **Feasible but deferred** (low priority, moderate effort)

**Coupling audit results:**

| Coupling Point | References Found | Severity |
|----------------|-----------------|----------|
| Compiler IL generation | **0** — no TypedArray references in 42 Compiler files | ✅ None |
| LinqExpressions builders | **0** — no TypedArray/ArrayBuffer references | ✅ None |
| BuiltIns (DataView) | **3** — `DataView` constructor requires `JSArrayBuffer` | ⚠️ Low |
| JSGlobal StructuredClone | **3** — `JSArrayBuffer` clone logic (lines 218–228) | ⚠️ Medium |
| Source generator registration | Via `[JSClassGenerator]` attributes (auto-wired) | ✅ Compatible |

**Key finding:** The original blocker ("Compiler generates IL that constructs
TypedArray instances") is **incorrect**. The Compiler has **zero references** to any
TypedArray or ArrayBuffer type. All TypedArray construction occurs at runtime through
the source-generated `RegisterGeneratedClasses()` mechanism, not via compiler-emitted
IL. This makes extraction significantly simpler than anticipated.

**Remaining challenges for extraction:**

1. **DataView → JSArrayBuffer dependency:** `DataView` (already in BuiltIns) has a
   direct dependency on `JSArrayBuffer`. If TypedArrays move to a separate assembly,
   BuiltIns would need to reference the new `Broiler.JavaScript.TypedArrays` assembly,
   or `JSArrayBuffer` would need to remain in Core (splitting the cohesive subsystem).

2. **JSGlobal StructuredClone:** `JSGlobal.StructuredClone` directly instantiates
   `new JSArrayBuffer(byte[])` for cloning. Would need a factory delegate similar
   to `StructuredCloneExtension`.

3. **Volume:** 1,830 LOC across 14 files — moderate migration effort.

**Extraction options:**

| Option | Approach | Effort | Benefit |
|--------|----------|--------|---------|
| A | Move all TypedArrays → new `Broiler.JavaScript.TypedArrays` assembly | Medium | Clean subsystem boundary |
| B | Move all TypedArrays → existing BuiltIns assembly | Small | Simpler dependency graph |
| C | Keep in Core | None | No disruption |

**Recommendation:** **Option C — Keep in Core.** The TypedArrays are a foundational
data type used by DataView and the engine's binary data handling. The Compiler has no
coupling (eliminating the assumed blocker), but extracting 1,830 LOC to save ~14 files
in Core provides minimal benefit relative to the DataView dependency and StructuredClone
factory delegate complexity. If Core slimming becomes a higher priority, Option B
(move to BuiltIns) is the lowest-effort path.

### 7.3 Potential Extraction — LinqExpressions → Compiler

The 41 LinqExpressions builder files form the "expression generation" layer that sits
between Core types and the Compiler. Conceptually they belong with the Compiler, but:

- Each builder references concrete Core types (`JSArray`, `JSString`, etc.)
- Moving them would require Core to expose all internal types to the Compiler assembly
  via `[InternalsVisibleTo]`, or the builders would need to work through interfaces
- This is a **large, high-risk refactor** with limited maintainability benefit

**Recommendation:** Defer. The LinqExpressions are tightly coupled to Core by design
and are not a maintainability concern in their current location.

---

## 8. Types Assessed as Impractical to Extract

The following types are **deeply integrated** into the Core engine and cannot be
practically extracted without fundamentally restructuring the engine:

| Type | Reason |
|------|--------|
| `JSArray` | Used pervasively by `JSContext`, `JSObject`, spread operations, `for...of` |
| `JSString` | Implicit string coercion in `JSValue.ToString()`, template literals |
| `JSNumber` | Arithmetic operators, type coercion, `JSValue.ToNumber()` |
| `JSError` | Exception handling, `try/catch` compilation, `JSException` |
| `JSPromise` | `async/await` compilation, `AsyncPump`, microtask queue |
| `JSRegExp` | String prototype methods (`match`, `replace`, `split`, `search`) |
| `JSObject` | Base class for all JS objects — foundational |
| `JSFunction` | Base class for all callable objects — foundational |
| `JSContext` | Engine orchestrator — owns prototype chain, built-in registry, global |
| `JSValue` | Already in Runtime as interface; concrete class must stay in Core |
| `JSGlobal` | Tightly coupled to `JSContext` initialization |
| `JSBoolean` | Used by type coercion in Core operators |
| `JSNull` / `JSUndefined` | Singleton primitives used everywhere |

---

## 9. Outstanding Tasks

### 9.1 Assembly Separation — Remaining Work

| # | Task | Priority | Effort | Status |
|---|------|----------|--------|--------|
| 1 | Extract `JSProxy` → BuiltIns | P3 | Small | ✅ **Complete (M2)** |
| 2 | Extract `JSJSON` / `JSJsonParser` → BuiltIns | P3 | Medium | ✅ **Complete (M3)** |
| 3 | Extract `JSDataView` / `DataViewStatic` → BuiltIns | P3 | Small | ✅ **Complete (M3)** |
| 4 | Extract `JSMath` → BuiltIns | P3 | Small | ✅ **Complete (M2)** |
| 5 | Extract `JSReflect` → BuiltIns | P3 | Small | ✅ **Complete (M2)** |
| 6 | Extract `JSConsole` → BuiltIns | P3 | Small | ✅ **Complete (M2)** |
| 7 | Extract `JSMap`/`JSWeakMap` → BuiltIns | P3 | Medium | ✅ **Complete (M3)** |
| 8 | Extract `JSSet`/`JSWeakSet` → BuiltIns | P3 | Medium | ✅ **Complete (M3)** |
| 9 | Evaluate `JSBigInt` extraction feasibility | P4 | Small | ✅ **Complete (M4)** — Extractable via factory delegate (see §7.1.1) |
| 10 | Evaluate TypedArrays as separate assembly | P4 | Large | ✅ **Complete (M4)** — Feasible but deferred; Compiler has zero coupling (see §7.2.1) |

### 9.2 Infrastructure — Gaps

| # | Task | Priority | Effort | Status |
|---|------|----------|--------|--------|
| 11 | Establish CI workflow for all assemblies | P1 | Medium | ✅ **Complete (M1)** |
| 12 | Create unit test projects for each assembly | P1 | Large | ✅ **Complete (M1)** — 12 test projects |
| 13 | Add integration test project | P2 | Medium | ✅ **Complete (M1)** |
| 14 | Align target frameworks (net8.0 vs net9.0) | P2 | Small | ✅ **Complete (M5)** — All projects aligned to net8.0; main exe changed from net9.0 |
| 15 | Add `Directory.Build.props` in `Broiler.JavaScript/` for shared settings | P3 | Small | ✅ **Complete (M5)** — Shared TFM (net8.0) and LangVersion (latest) |

> **Note:** M1 is complete. 12 test projects and CI workflow are in place.
> 93 tests pass across all 12 projects on ubuntu, windows, and macOS.

### 9.3 Documentation — Gaps

| # | Task | Priority | Effort | Status |
|---|------|----------|--------|--------|
| 16 | Document assembly architecture diagram | P2 | Small | ✅ **Complete (M5)** — This document |
| 17 | Document migration guide for downstream consumers | P3 | Medium | ✅ **Complete (M5)** — See §14 |
| 18 | Document `[ModuleInitializer]` wiring patterns | P2 | Small | ✅ **Complete (M5)** — Documented in Section 4 |

---

## 10. Actionable Steps

### Phase 1 — Infrastructure (P1)

**Goal:** Establish CI and test coverage for the separated architecture.

1. **Create CI workflow** (`.github/workflows/ci.yml`)
   - Build all projects on ubuntu, windows, macos
   - Run any existing tests
   - Code coverage with coverlet

2. **Add unit test projects** for assemblies lacking them:
   - `Broiler.JavaScript.Core.Tests`
   - `Broiler.JavaScript.Runtime.Tests`
   - `Broiler.JavaScript.Storage.Tests`
   - `Broiler.JavaScript.Ast.Tests`
   - `Broiler.JavaScript.Parser.Tests`
   - `Broiler.JavaScript.Compiler.Tests`
   - `Broiler.JavaScript.BuiltIns.Tests`
   - `Broiler.JavaScript.Clr.Tests`
   - `Broiler.JavaScript.Debugger.Tests`
   - `Broiler.JavaScript.Modules.Tests`
   - `Broiler.JavaScript.ModuleExtensions.Tests`
   - `Broiler.JavaScript.Integration.Tests`

3. **Verify all assemblies build** across platforms.

### Phase 2 — Low-Hanging Fruit Extractions (P3)

**Goal:** Move standalone built-in types from Core to BuiltIns.

For each type, follow this pattern:
1. Move source file(s) to `Broiler.JavaScript.BuiltIns/`
2. Keep namespace unchanged for source compatibility
3. Wire factory delegate in `BuiltInsAssemblyInitializer` if needed (e.g., `ConsoleFactory`)
4. Update `DefaultBuiltInRegistry` if the type is registered there
5. Add unit tests and integration tests

> **Note on TypeForwardedTo:** `[TypeForwardedTo]` cannot be used for Core→BuiltIns
> extractions because BuiltIns references Core (the reverse direction). Binary
> compatibility is maintained by preserving namespaces and ensuring all dependent
> projects reference BuiltIns.

**M2 completed (Tasks 1, 4, 5, 6):**
1. ~~`JSMath`~~ ✅ — stateless, no dependencies beyond `JSValue`
2. ~~`JSReflect`~~ ✅ — stateless, delegates to `JSObject` methods
3. ~~`JSConsole`~~ ✅ — wired via `ConsoleFactory` delegate
4. ~~`JSProxy`~~ ✅ — no Compiler references

**Remaining for M3:**
5. ~~`JSDataView` + `DataViewStatic`~~ ✅ — self-contained
6. ~~`JSMap` / `JSWeakMap`~~ ✅ — wired via `StructuredCloneExtension` delegate
7. ~~`JSSet` / `JSWeakSet`~~ ✅ — wired via `StructuredCloneExtension` delegate
8. ~~`JSJSON` / `JSJsonParser`~~ ✅ — Network already references BuiltIns

### Phase 3 — Evaluation & Planning (P4) ✅ Complete (M4)

**Goal:** Assess remaining extraction candidates.

1. **Audit Compiler IL generation** to determine which Core types are directly
   referenced in emitted IL (blocking extraction without factory delegates).
   ✅ **Done** — JSBigInt is referenced via `JSBigIntBuilder` (2 call sites in
   Compiler). TypedArrays have **zero** Compiler references (original blocker
   was incorrect).

2. **Evaluate TypedArrays** as a separate `Broiler.JavaScript.TypedArrays` assembly:
   - ✅ Analyze `JSTypedArray` base class coupling to `JSObject` — extends `JSObject`
   - ✅ Check Compiler references to typed array constructors — **none found**
   - ✅ Estimate factory delegate complexity — only needed for StructuredClone
   - **Recommendation:** Keep in Core (see §7.2.1)

3. **Evaluate `JSBigInt`** extraction:
   - ✅ Check Compiler literal creation references — `JSBigIntBuilder.New()` (2 sites)
   - ✅ Determine if a factory delegate is sufficient — **yes**, follows JSDecimal pattern
   - **Recommendation:** Extractable; defer unless Core slimming is prioritized (see §7.1.1)

4. **Evaluate LinqExpressions** extraction to Compiler:
   - Previously assessed in §7.3: **Deferred** — tightly coupled by design, not a
     maintainability concern.

### Phase 4 — Documentation & Polish (P2–P3) ✅ Complete (M5)

1. **Create migration guide** for downstream consumers moving from monolithic
   to separated assembly references. ✅ See §14.

2. **Update `Broiler.JavaScript.All` meta-package** to include any new assemblies.
   ✅ Already includes all assemblies (Core, Clr, Compiler, Modules, BuiltIns, Debugger).

3. **Add XML documentation** to all public interfaces in Runtime.
   ✅ Key public interfaces documented.

4. **Align TFMs**: ✅ All projects standardized on `net8.0`. Main executable changed
   from `net9.0` to `net8.0`. CI workflow updated from `dotnet-version: 9.0.x`
   to `8.0.x`. Shared `Directory.Build.props` added in `Broiler.JavaScript/`.

---

## 11. Milestones & Timeline

| Milestone | Tasks | Estimated Effort | Target | Status |
|-----------|-------|-----------------|--------|--------|
| **M1 — CI & Test Foundation** | Tasks 11–13 | 2–3 days | Week 1 | ✅ **Complete** — see [milestone-1-plan.md](./milestone-1-plan.md) |
| **M2 — Quick Wins** | Tasks 1, 4, 5, 6 (Proxy, Math, Reflect, Console) | 1–2 days | Week 2 | ✅ **Complete** — 4 types extracted, 93 tests passing |
| **M3 — Medium Extractions** | Tasks 2, 3, 7, 8 (JSON, DataView, Map, Set) | 2–3 days | Week 3 | ✅ **Complete** — 8 types extracted, `StructuredCloneExtension` delegate added, 116 tests passing |
| **M4 — Evaluation** | Tasks 9, 10 (BigInt, TypedArrays feasibility) | 1 day | Week 3 | ✅ **Complete** — JSBigInt extractable via factory delegate; TypedArrays feasible but deferred (zero Compiler coupling found) |
| **M5 — Documentation** | Tasks 14–18 (TFM alignment, migration guide, docs) | 1–2 days | Week 4 | ✅ **Complete** — All projects aligned to net8.0; Directory.Build.props added; CI updated to 8.0.x; 116 tests passing |
| **M6 — Final Validation** | Full regression testing, performance benchmarks | 1 day | Week 4 | Not started |

**Total estimated effort:** 8–12 working days

---

## 12. Risks & Mitigations

| Risk | Impact | Likelihood | Mitigation |
|------|--------|-----------|------------|
| Extracting a type breaks Compiler IL generation | High | Medium | Audit Compiler references before extraction; use factory delegates. **M4 finding:** TypedArrays have zero Compiler coupling; JSBigInt has 2 Compiler references (manageable via factory delegate) |
| TypeForwardedTo attributes cause assembly loading issues | Medium | Low | Test on all target platforms (Windows, Linux, macOS) |
| Source generator `Names.g.cs` conflicts between assemblies | Medium | Low | Already proven safe — Core and BuiltIns both have `Names` classes |
| Performance regression from factory delegate indirection | Low | Low | Benchmark critical paths; delegates are one-time initialization |
| Breaking change for downstream consumers | High | Low | TypeForwardedTo provides binary compatibility; document in migration guide |
| No existing tests to catch regressions | ~~High~~ | ~~High~~ | ✅ Mitigated — 116 tests across 12 projects (M1–M3) |

---

## 13. Appendix — File Counts by Assembly

### Core Assembly Breakdown (~186 files)

```
Broiler.JavaScript.Core/
├── Core/                    104 files  ← built-in JS types
│   ├── Array/                19 files  (incl. 15 TypedArray)
│   ├── String/                5 files
│   ├── Promise/               5 files
│   ├── Generator/             5 files
│   ├── Object/                4 files
│   ├── Date/                  4 files
│   ├── Clr/                   4 files
│   ├── Primitive/             4 files
│   ├── Number/                3 files
│   ├── Symbol/                3 files
│   ├── Storage/               3 files
│   ├── Module/                3 files
│   ├── Map/                   2 files
│   ├── Set/                   2 files
│   ├── RegExp/                2 files
│   ├── Json/                  2 files
│   ├── DataView/              2 files
│   ├── Function/              2 files
│   ├── BigInt/                1 file
│   ├── Boolean/               1 file
│   ├── Class/                 1 file
│   ├── Iterator/              1 file
│   ├── Global/                1 file
│   └── Root (Context, etc.)  ~10 files
│
├── LinqExpressions/          41 files  ← IL expression builders
│   ├── Root                  33 files
│   └── GeneratorsV2/          8 files
│
├── Extensions/               10 files
├── Utils/                     9 files
├── Emit/                      2 files
├── CodeGen/                   2 files
├── FastParser/Compiler/       1 file
├── LambdaGen/                 1 file
├── Debugger/                  1 file
├── Enumerators/               1 file
├── Parser/                    1 file
├── TypeQuery/                 1 file
└── Root (forwarding, etc.)  ~16 files
```

> **M2 Note:** `Objects/` (JSMath, JSReflect), `Proxy/`, and `Debug/` directories
> were removed from Core after extraction to BuiltIns.
>
> **M3 Note:** `DataView/`, `Map/`, `Set/`, and `Json/` directories were removed
> from Core after extraction to BuiltIns. A `StructuredCloneExtension` delegate
> was added to `DefaultBuiltInRegistry` so `JSGlobal.StructuredClone` can clone
> Map/Set values without Core referencing BuiltIns.
>
> **M4 Note:** Evaluation complete. JSBigInt (1 file in `BigInt/`) is extractable
> via factory delegate pattern (like JSDecimal). TypedArrays (14 files in
> `Array/Typed/`) have zero Compiler coupling — original blocker was incorrect —
> but extraction is deferred due to DataView dependency and low ROI.

### Summary of Extraction Progress

| Category | In Core | Extracted | Extractable | Impractical |
|----------|---------|-----------|-------------|-------------|
| Built-in types | ~68 | 24 (BuiltIns) | ~1 | ~55 |
| AST types | 0 | 18 (Ast) | — | — |
| Parser types | 0 | 12 (Parser) | — | — |
| Storage types | 0 | 10 (Storage) | — | — |
| Runtime types | 0 | 26 (Runtime) | — | — |
| IL Builders | 41 | 0 | Deferred | — |
| Utilities | ~30 | 0 | ~5 | ~25 |
| **Total** | **~139** | **90** | **~6** | **~80** |

---

## 14. Migration Guide — .NET 8.0 TFM Alignment (M5)

### What Changed

1. **Target Framework:** The main executable (`Broiler.JavaScript.csproj`) was
   changed from `net9.0` to `net8.0`. All library and test projects were already
   `net8.0` — no changes needed.

2. **CI Workflow:** `.github/workflows/ci.yml` updated `dotnet-version` from
   `9.0.x` to `8.0.x` to match the unified TFM.

3. **Package Version:** `Microsoft.Extensions.DependencyModel` downgraded from
   `9.0.6` to `8.0.2` for consistency with the net8.0 target.

4. **Shared Build Properties:** A new `Broiler.JavaScript/Directory.Build.props`
   was added with shared defaults:
   - `TargetFramework` = `net8.0` (as a fallback; individual projects can override)
   - `LangVersion` = `latest`

### Breaking Changes

- **Minimum runtime:** Applications must run on .NET 8.0 or later (previously
  the main executable required .NET 9.0).
- **No API changes:** All public APIs remain unchanged.
- **No namespace changes:** All namespaces are preserved.

### For Downstream Consumers

- If you reference individual assemblies (e.g., `Broiler.JavaScript.Core`),
  no changes are required — they were already `net8.0`.
- If you reference the main executable or `Broiler.JavaScript.All` meta-package,
  ensure your project targets `net8.0` or later.
- The `[ModuleInitializer]` wiring pattern is unchanged (see Section 4).

---

*Last updated: 2026-03-22 — M5 complete (TFM alignment to net8.0, Directory.Build.props, CI update, migration guide)*
