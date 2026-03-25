# Broiler.JavaScript — Modular Architecture

This document describes the final modular architecture of the
Broiler.JavaScript engine after the Core-to-modules refactor.  It is
intended as the **primary reference** for contributors and maintainers.

> **Historical context.** The engine originally shipped as a single
> monolithic `Broiler.JavaScript.Core` assembly.  Over several
> milestones the code was decomposed into purpose-built assemblies.  For
> the full migration history see
> [`docs/roadmap/javascript-engine-assembly-refactor.md`](../roadmap/javascript-engine-assembly-refactor.md).

---

## 1. Design Principles

| # | Principle | Rationale |
|---|-----------|-----------|
| 1 | **One responsibility per assembly.** | Keeps compile times short and makes it easy to reason about dependencies. |
| 2 | **Dependency flow is strictly downward.**  Feature → Core → Foundation.  No circular references. | Prevents tangled builds and unpredictable initialization order. |
| 3 | **Upward communication through delegates and interfaces.** | Feature assemblies register factories into Core's static properties at module-initialization time so Core never references Feature assemblies directly. |
| 4 | **Namespace preservation across moves.** | Extracted types retain their original namespace (e.g., `Broiler.JavaScript.Core.Core`) to keep source compatibility. |
| 5 | **Type forwarding for binary compatibility.** | `[assembly: TypeForwardedTo(...)]` in Core ensures downstream consumers that reference Core continue to resolve types that moved to Foundation assemblies. |

---

## 2. Assembly Map

### 2.1 Foundation Layer

These assemblies sit at the bottom of the dependency graph and have
**no upward dependencies** (they never reference Core or Feature assemblies).

| Assembly | Purpose |
|----------|---------|
| **ExpressionCompiler** | LINQ expression-tree compilation infrastructure.  Leaf dependency—most other assemblies reference it. |
| **Ast** | JavaScript AST node types and token definitions.  Depends on ExpressionCompiler. |
| **Storage** | Memory management primitives (`VirtualMemory`, `VirtualArray`, concurrent maps, `PropertySequence`, `KeyString`).  Depends on ExpressionCompiler. |
| **Runtime** | Core value types (`JSValue`, `JSObject`, `Arguments`, `PropertyKey`), function delegate types, prototype/symbol contracts, iterator interfaces.  Depends on Ast and ExpressionCompiler. |
| **Parser** | JavaScript lexer and parser (`FastParser`, `FastScanner`, `FastTokenStream`).  Depends on Ast. |

### 2.2 Core Layer

| Assembly | Purpose |
|----------|---------|
| **Core** | Central wiring assembly.  Contains `JSEngine` (static access to the current execution context), `IJSExecutionContext`, `JSException`, exception/value factory delegates, module initializers, `ObjectClassFactory`, primitive types (`JSUndefined`, `JSPrimitiveObject`), `CallStackItem`, the `DefaultJSCompiler` adapter, and the `JSDebugger` base class.  Depends on Parser, Storage, Runtime, ExpressionCompiler. |

Core's role is **coordination, not business logic**.  It provides the
delegate infrastructure that lets Feature assemblies plug into the
engine without creating circular references.

### 2.3 Feature Layer

Each assembly depends on Core (and optionally on other Feature
assemblies) and implements a specific capability:

| Assembly | Purpose | Initialization |
|----------|---------|----------------|
| **Engine** | Concrete `JSContext` implementation (execution context, global scope, prototype bootstrapping). | — |
| **LinqExpressions** | LINQ expression-tree builders for JS operations (arithmetic, comparison, coercion, property access, function calls), `TypeQuery` utility, template-string builder. | `LinqExpressionsAssemblyInitializer` wires `JSFunction.CreateClrDelegateFactory` and `JSValueToClrConverter` delegates. |
| **BuiltIns** | Extracted built-in JavaScript objects (Array, Map, Set, Date, BigInt, Boolean, Number, String, Symbol, RegExp, Promise, Proxy, Error, JSON, Math, Reflect, Intl, Console, DataView, WeakRef, Iterator, Generator, Disposable, Events, etc.) and `DefaultBuiltInRegistry`. | `BuiltInsAssemblyInitializer` registers all built-in classes and factory delegates. |
| **Extensions** | JSValue/JSObject extension methods, CLR proxy extensions, string/marshal extensions. | `ExtensionsAssemblyInitializer`. |
| **Globals** | Global functions (`parseInt`, `parseFloat`, `isNaN`, `isFinite`, `eval`, `encodeURI`, `decodeURI`). | `GlobalsAssemblyInitializer`. |
| **Compiler** | `FastCompiler` JIT/compilation pipeline (statement visitors, expression visitors, declaration handlers, scope management). | `CompilerAssemblyInitializer` registers the compilation function via `DefaultJSCompiler.Register`. |
| **Clr** | CLR/.NET interop (proxy creation, expression building, CSX module support). | `ClrAssemblyInitializer` wires `IClrInterop` and `ClrModuleProvider`. |
| **Debugger** | Debugging infrastructure (V8 Inspector Protocol, breakpoints, stepping, scope introspection). | — |
| **Modules** | ESM/CommonJS module system, module loading and resolution. | — |
| **ModuleExtensions** | Fluent API for module registration. | — |
| **JSClassGenerator** | Roslyn source generator for `[PrototypeAttribute]` / `[JSRegistrationGenerator]` built-in class registration code.  Referenced as an analyzer, not a runtime dependency. | — |

### 2.4 Integration & Application Layer

| Assembly | Purpose |
|----------|---------|
| **All** | Meta-package that transitively includes all engine assemblies for consumers who want the full engine. |
| **Broiler.JavaScript** (CLI) | Command-line REPL and script runner. |
| **Network** | Network utilities (fetch, WebSocket, FormData). |
| **NodePollyfill** | Node.js compatibility polyfills. |

---

## 3. Dependency Graph

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
  Debugger ─────────► Core, BuiltIns, Engine
  Extensions ───────► Core, LinqExpressions, Engine
  Globals ──────────► Core, BuiltIns, Extensions, Engine
  Clr ──────────────► Core, LinqExpressions
  BuiltIns ─────────► Core, LinqExpressions, Extensions, Engine
  Compiler ─────────► Core, LinqExpressions
  LinqExpressions ──► Core, Runtime, ExpressionCompiler, Storage, Ast
  Engine ───────────► Core

Core Layer
  Core ──► Parser, Storage, Runtime, ExpressionCompiler, Ast

Foundation Layer
  Parser ──► Ast
  Storage ──► ExpressionCompiler
  Runtime ──► Ast, ExpressionCompiler
  Ast ──► ExpressionCompiler
  ExpressionCompiler (leaf — no internal dependencies)
```

---

## 4. Cross-Assembly Communication

### 4.1 Module Initializers

Satellite assemblies use `[ModuleInitializer]` methods to register
factories and delegates **without** requiring Core to reference them:

| Initializer | Assembly | Wiring |
|-------------|----------|--------|
| `BuiltInsAssemblyInitializer` | BuiltIns | Registers built-in classes, error factories, promise factories, `JSRegExpBuilder`, `JSFunctionBuilder`, string/number/boolean factories, etc. |
| `LinqExpressionsAssemblyInitializer` | LinqExpressions | Wires `JSFunction.CreateClrDelegateFactory` and `JSValueToClrConverter` delegates. |
| `ExtensionsAssemblyInitializer` | Extensions | Registers extension-method hooks. |
| `GlobalsAssemblyInitializer` | Globals | Registers global-function hooks. |
| `CompilerAssemblyInitializer` | Compiler | Registers `DefaultJSCompiler` compilation function. |
| `ClrAssemblyInitializer` | Clr | Wires `IClrInterop`, `ClrModuleProvider`. |
| `CoreAssemblyInitializer` | Core | Wires `CoreClassRegistrations`, `CreateObjectClass`, `JSObject` factory delegates. |
| `CoreScriptCoreExtensions` | Core | Wires `CoreScript` factory delegates (compiler, code cache, context, async pump, syntax error). |
| `JSValueCoreExtensions` | Core | Wires `JSValue.UndefinedValue`, `NewTypeError`, `CreateDynamicMetaObject`, `ForceConvertHelper`, `InvokePropertyGetter`, `CreatePrototypeObject`, `Arguments.Empty`, `Arguments` delegate impls. |
| `PropertySequenceCoreExtensions` | Core | Wires `PropertySequence.TypeErrorFactory`. |

### 4.2 Type Forwarding

Core maintains four type-forwarding files to preserve binary
compatibility for types extracted to Foundation assemblies:

| File | Forwards To | Count |
|------|-------------|-------|
| `ClrTypeForwarding.cs` | Runtime | 9 types (JSExportAttribute, DictionaryCodeCache, MethodProvider, IJavaScriptObject, etc.) |
| `ObjectTypeForwarding.cs` | Runtime | 8 types (JSObject, JSObjectStatic, KeyEnumerator, JSIterator, etc.) |
| `ParserTypeForwarding.cs` | Parser | 14 types (FastParser, FastScanner, Error, ErrorHandler, etc.) |
| `StorageTypeForwarding.cs` | Storage | 10 types (VirtualMemory, StringMap, ConcurrentNameMap, etc.) |

> **Note:** Type forwarding is **not** used for types moved to BuiltIns,
> Extensions, or LinqExpressions because those assemblies depend on Core
> (adding a reverse forwarding reference would create a circular
> dependency).  Consumers that reference those types must add the
> appropriate project/package reference.

### 4.3 Interface & Delegate Contracts

Core defines contracts that Feature assemblies implement:

| Contract | Location | Implementors |
|----------|----------|-------------- |
| `IClrInterop` | Runtime | Clr (`ClrProxy`) |
| `IBuiltInRegistry` | Runtime | BuiltIns (`DefaultBuiltInRegistry`) |
| `IJSCompiler` | Runtime | Core (`DefaultJSCompiler`), Compiler (via `Register` delegate) |
| `ICodeCache` | Runtime | Runtime (`DictionaryCodeCache`) |
| `IDebugger` | Runtime | Core (`JSDebugger`), Debugger (`V8InspectorProtocol`) |
| `IJSExecutionContext` | Core | Engine (`JSContext`) |
| `IJSError` | Runtime | BuiltIns (`JSError`) |
| `IJSPromise` | Runtime | BuiltIns (`JSPromise`) |
| `IJSRegExp` | Runtime | BuiltIns (`JSRegExp`) |

---

## 5. What Remains in Core

After the refactor, Core retains **~44 source files** (down from the
original ~180).  Every remaining file exists because it bridges
assemblies that cannot reference each other or provides coordination
logic that genuinely belongs at the center:

| Category | Files | Reason It Stays |
|----------|-------|-----------------|
| **Engine statics** | `JSEngine.cs` | Thread-local context, error factory delegates, assembly-loading logic.  Cannot move to Engine because Foundation types reference `JSEngine.Current`. |
| **Execution context** | `IJSExecutionContext.cs`, `JSContextExtensions.cs`, `CallStackItem.cs` | Core types need the contract; the concrete `JSContext` is in Engine. |
| **Exception infrastructure** | `JSException.cs` | Depends on `JSEngine`, `IJSError`, `KeyStrings`.  Tightly coupled to the engine's error model. |
| **Variable model** | `JSVariable.cs` | Used by compiled closures; depends on `JSEngine`, `JSException`. |
| **Primitive types** | `JSUndefined.cs`, `JSPrimitiveObject.cs` | `JSUndefined.Value` is wired into `JSValue.UndefinedValue` at startup; `JSPrimitiveObject` wraps primitives as objects. |
| **Object class factory** | `ObjectClassFactory.cs`, `Names.cs` | Creates the JavaScript `Object` constructor.  Uses `JSValue.CreateFunction` delegate to avoid referencing BuiltIns. |
| **Module initializers** | `CoreAssemblyInitializer.cs`, `CoreScriptCoreExtensions.cs`, `JSValueCoreExtensions.cs` | Wire delegates between Core and Runtime/Foundation. |
| **Bridge extensions** | `KeyStrings.cs` (KeyStringCoreExtensions), `ArgumentsCoreExtensions.cs`, `PropertySequenceCoreExtensions.cs`, `JSPropertyFactory.cs` | Extend Foundation types with Core-dependent logic (e.g., converting `KeyString` to `JSValue`). |
| **JSValue extensions** | `Extensions/JSValueExtensions.cs`, `Extensions/JSValueInvokeMethod*.cs` | Method-invocation overloads that depend on Core types. |
| **Internal helpers** | `Extensions/InternalExtensionHelpers.cs` (CoreInternalHelpers) | Minimal CLR-enumeration and unmarshal helpers for Core call sites that cannot reference Extensions. |
| **Compiler adapter** | `FastParser/Compiler/DefaultJSCompiler.cs` | Adapter that loads the Compiler assembly lazily and forwards compile requests. |
| **Debugger base** | `Debugger/JSDebugger.cs` | Abstract base class for debugger implementations; used by LinqExpressions' `JSDebuggerBuilder`. |
| **DLR integration** | `JSDynamicMetaData.cs` | C# `dynamic` keyword support.  Internal to Core. |
| **Utilities** | `AsyncPump.cs`, `NumberParser.cs`, `DateParser.cs`, `BigIntegerExtensions.cs`, `UriHelper.cs`, `TypeConverter.cs`, `ArgumentsExtension.cs` (JSValueToClrConverter) | Used across multiple assemblies (BuiltIns, Globals, Compiler, Clr). |
| **Enumerators** | `KeyEnumerator.cs` (typed), `OwnEntriesEnumerator.cs` | Element-enumerator implementations used by BuiltIns and Runtime. |
| **CLR interop** | `IJavaScriptObject.cs` (JavaScriptObject abstract class) | Depends on `JSEngine.ClrInterop`; the interface itself is in Runtime. |
| **Template strings** | `JSTemplateString.cs` | Referenced by `JSTemplateStringBuilder` in LinqExpressions. |
| **Unique ID** | `UniqueID.cs` | Internal extension on JSValue for identity keying. |
| **Attributes** | `PrototypeAttribute.cs` | `[Prototype]`, `[GetProperty]`, `[SetProperty]`, `[Static]` — used by source-generated code across assemblies. |
| **Type forwarding** | `ClrTypeForwarding.cs`, `ObjectTypeForwarding.cs`, `ParserTypeForwarding.cs`, `StorageTypeForwarding.cs` | Binary compatibility shims. |
| **Namespace anchors** | `FunctionNamespace.cs` | Provides the `Broiler.JavaScript.BuiltIns.Function` namespace within Core so source-generated code compiles (the generator emits a `using` directive for this namespace). |
| **Assembly info** | `AssemblyInfo.cs` | `InternalsVisibleTo` declarations. |

---

## 6. Known Workarounds & Technical Debt

| Item | Location | Description | Recommended Fix |
|------|----------|-------------|-----------------|
| **FunctionNamespace.cs** | `Core/Function/` | Empty namespace declaration that satisfies a `using` directive emitted by the source generator. | Update `JSClassGenerator` to omit unused `using` directives. |
| **CoreInternalHelpers duplication** | `Core/Extensions/InternalExtensionHelpers.cs` vs `Extensions/InternalExtensionHelpers.cs` | Minimal duplicate of `TryGetClrEnumerator` and `TryUnmarshal` in Core because Core cannot reference Extensions. | Consider moving helpers to Runtime if they can be decoupled from Core types. |
| **Null-initialized static delegates** | `JSEngine.cs`, `JSException.cs`, `JSValueToClrConverter` | Factory delegates are `null` until a module initializer runs.  If initialization is missed, `NullReferenceException` occurs at runtime. | Add null-guard diagnostics or convert to lazy-initialized patterns. |
| **`NotImplementedException` stubs** | `OwnEntriesEnumerator.cs` (`ClrObjectEnumerator<T>.Dispose()` / `.Reset()`) | IEnumerator contract methods throw `NotImplementedException`. | Provide no-op implementations or throw `NotSupportedException`. |
| **`PrototypeAttribute` in Core** | `Core/PrototypeAttribute.cs` | Public attribute used by source-generated code across all assemblies.  Should ideally live in Runtime or a dedicated Attributes assembly, but moving it would require all consumers to add a new reference. | Consider moving to Runtime with type forwarding (since Core already references Runtime). |

---

## 7. Adding a New Module

1. **Create the project** under `Broiler.JavaScript/`:
   ```
   dotnet new classlib -n Broiler.JavaScript.MyFeature \
     --framework net8.0 -o Broiler.JavaScript/Broiler.JavaScript.MyFeature
   ```
2. **Add project references** to Core (and optionally LinqExpressions, Extensions, Engine).
3. **Add `InternalsVisibleTo`** in Core's `AssemblyInfo.cs` and Runtime's `AssemblyInfo.cs`.
4. **Register via `[ModuleInitializer]`** if the module needs to wire factory delegates into Core.
5. **Add the project to `YantraJS.sln`** and to `Broiler.JavaScript.All` for the meta-package.
6. **Create a test project** under `Broiler.JavaScript/Broiler.JavaScript.MyFeature.Tests/`.

For built-in type extraction specifically, see
[`docs/architecture/extraction-pattern.md`](extraction-pattern.md).

---

## 8. Test Coverage

| Test Project | Tests | Status |
|-------------|-------|--------|
| Broiler.JavaScript.Ast.Tests | 5 | ✅ Passing |
| Broiler.JavaScript.Parser.Tests | 5 | ✅ Passing |
| Broiler.JavaScript.Runtime.Tests | 7 | ✅ Passing |
| Broiler.JavaScript.Core.Tests | — | ⚠️ Pre-existing build errors (missing Engine reference) |
| Broiler.JavaScript.Compiler.Tests | — | ⚠️ Pre-existing build errors (missing Engine reference) |
| Broiler.JavaScript.BuiltIns.Tests | — | ⚠️ Pre-existing build errors (missing Engine reference) |
| Broiler.JavaScript.Clr.Tests | — | ⚠️ Pre-existing build errors (missing Engine reference) |
| Broiler.JavaScript.Debugger.Tests | — | ⚠️ Pre-existing build errors (missing Engine reference) |
| Broiler.JavaScript.Modules.Tests | — | ⚠️ Pre-existing build errors (missing Module namespace) |
| Broiler.JavaScript.ModuleExtensions.Tests | — | ⚠️ Pre-existing build errors (missing Arguments) |
| Broiler.JavaScript.Storage.Tests | — | ⚠️ Pre-existing build errors (missing Core ref) |
| Broiler.JavaScript.Integration.Tests | — | ⚠️ Pre-existing build errors |

> **Note:** The pre-existing test build errors in many test projects are
> caused by those projects still referencing `JSContext` and other types
> via old namespace paths.  Fixing them requires adding `Engine` project
> references and updating `using` directives — a task tracked separately
> from the Core-to-modules refactor.
