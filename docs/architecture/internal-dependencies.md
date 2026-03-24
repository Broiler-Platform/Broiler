# Internal Dependencies — Broiler.JavaScript (JavaScript-Engine)

This document provides a comprehensive reference for all internal dependencies
within the Broiler.JavaScript engine. It covers assembly-to-assembly project
references, external (NuGet) package dependencies, factory-delegate and
interface contracts that cross assembly boundaries, fallback behaviour when
optional assemblies are absent, and the rules that prevent circular references.

> **Companion documents**
>
> - [Assembly Refactor Roadmap](../roadmap/javascript-engine-assembly-refactor.md) — milestones, rationale, and history.
> - [Extraction Pattern](extraction-pattern.md) — step-by-step guide for moving types between assemblies.
> - [Module Initializers](module-initializers.md) — startup sequence and initializer chain.
> - [Contributing Built-Ins](contributing-builtins.md) — how to add new built-in types.

---

## 1. Assembly Dependency Matrix

Each row lists the assembly and the project references it declares.
Tooling-only references (source generators with `ReferenceOutputAssembly=false`)
are marked with *(analyzer)*.

| Assembly | Direct Project References |
|----------|--------------------------|
| **ExpressionCompiler** | *(none — leaf dependency)* |
| **JSClassGenerator** | *(none — source generator)* |
| **Ast** | ExpressionCompiler |
| **Storage** | Ast |
| **Parser** | Ast, ExpressionCompiler |
| **Runtime** | Ast, ExpressionCompiler, Storage |
| **Core** | Ast, Parser, Storage, Runtime, ExpressionCompiler, JSClassGenerator *(analyzer)* |
| **Compiler** | Core, JSClassGenerator *(analyzer)* |
| **BuiltIns** | Core |
| **Clr** | Core |
| **Debugger** | Core |
| **Modules** | Core, JSClassGenerator *(analyzer)* |
| **ModuleExtensions** | Core, Clr, ExpressionCompiler, Modules |
| **Network** | Core, BuiltIns, Clr, JSClassGenerator *(analyzer)* |
| **NodePollyfill** | Core |
| **All** *(meta-package)* | Core, Clr, Compiler, Modules, ModuleExtensions, BuiltIns, Debugger |
| **Broiler.JavaScript** *(CLI)* | Core, Clr, ExpressionCompiler, Modules, Network |

### 1.1 Dependency Graph (ASCII)

```
Application Layer
  Broiler.JavaScript (CLI)
    └─► Clr, Core, ExpressionCompiler, Modules, Network

Integration Layer
  Broiler.JavaScript.All (meta-package)
    └─► Core, Clr, Compiler, Modules, ModuleExtensions, BuiltIns, Debugger

Feature Layer
  ModuleExtensions ──► Clr, Core, ExpressionCompiler, Modules
  Network ───────────► BuiltIns, Clr, Core
  Modules ───────────► Core
  Debugger ──────────► Core
  Clr ───────────────► Core
  Compiler ──────────► Core
  BuiltIns ──────────► Core
  NodePollyfill ─────► Core

Core Layer
  Core ──► Ast, Parser, Storage, Runtime, ExpressionCompiler

Foundation Layer
  Runtime ──► Ast, ExpressionCompiler, Storage
  Parser ───► Ast, ExpressionCompiler
  Storage ──► Ast
  Ast ──────► ExpressionCompiler
  ExpressionCompiler ──► (no project deps)
```

### 1.2 Layered Dependency Rules

| Layer | May Reference | Must Not Reference |
|-------|---------------|--------------------|
| Foundation | Other Foundation assemblies at the same or lower level | Core, Feature, Integration, Application |
| Core | Foundation | Feature, Integration, Application |
| Feature | Core, Foundation | Other Feature assemblies (except Network → BuiltIns, ModuleExtensions → Clr + Modules) |
| Integration / Application | Any lower layer | — |

> **Exceptions:** `ModuleExtensions` references `Clr` and `Modules` (both Feature-layer)
> because it provides a unified fluent API that wraps both subsystems.
> `Network` references `BuiltIns` and `Clr` for typed array and CLR interop support.
> These exceptions are intentional and do not create cycles.

---

## 2. External (NuGet) Package Dependencies

Packages inherited via `Directory.Build.props` apply to every project and are
not repeated below.

| Assembly | NuGet Package | Version | Purpose |
|----------|---------------|---------|---------|
| **ExpressionCompiler** | System.Reflection.Emit | 4.7.0 | IL emission |
| | System.Reflection.Emit.Lightweight | 4.7.0 | DynamicMethod support |
| | System.Reflection.Metadata | 10.0.3 | Metadata reading |
| | Nerdbank.GitVersioning | 3.9.50 | Versioning |
| **Core** | ErrorProne.NET.Structs | 0.3.0-beta.0 | Struct analysis |
| | Microsoft.CodeAnalysis.Analyzers | 4.14.0 | Roslyn analyzers |
| | System.Text.Json | 10.0.3 | JSON support |
| | System.Reflection.Emit.Lightweight | 4.7.0 | DynamicMethod support |
| | System.Runtime | 4.3.1 | Runtime extensions |
| | Nerdbank.GitVersioning | 3.9.50 | Versioning |
| **JSClassGenerator** | Microsoft.CodeAnalysis.CSharp.Workspaces | 5.0.0 | Source generation |
| | Nerdbank.GitVersioning | 3.9.50 | Versioning |
| **Broiler.JavaScript** *(CLI)* | NuGet.Client | 4.2.0 | Package management |
| | Microsoft.CodeAnalysis.CSharp.Scripting | 4.14.0 | C# scripting |
| | NuGet.Protocol | 6.14.0 | NuGet feeds |
| | NuGet.Resolver | 6.14.0 | Dependency resolution |
| | Microsoft.Extensions.DependencyModel | 8.0.2 | Runtime dependency model |
| | Nerdbank.GitVersioning | 3.7.115 | Versioning |
| **ModuleExtensions** | Nerdbank.GitVersioning | 3.7.115 | Versioning |
| **Network** | Nerdbank.GitVersioning | 3.7.115 | Versioning |
| **NodePollyfill** | Nerdbank.GitVersioning | 3.7.115 | Versioning |

### 2.1 Test-Only Packages (all test projects)

| Package | Version | Purpose |
|---------|---------|---------|
| Microsoft.NET.Test.Sdk | 17.8.0 | Test host |
| xunit | 2.5.3 | Test framework |
| xunit.runner.visualstudio | 2.5.3 | Test discovery |
| coverlet.collector | 6.0.0 | Code coverage |

---

## 3. Cross-Assembly Contracts

Satellite assemblies communicate with Core (and Foundation) through three
mechanisms: **interfaces**, **factory delegates**, and **module initializers**.

### 3.1 Interfaces

| Interface | Declared In | Implemented By | Purpose |
|-----------|-------------|----------------|---------|
| `IClrInterop` | Runtime | `FallbackClrInterop` (Core), `DefaultClrInterop` (Clr) | Marshal .NET objects ↔ JS values |
| `IJSCompiler` | Runtime | `DefaultJSCompiler` (Core → Compiler) | Compile JS source to executable code |
| `IBuiltInRegistry` | Runtime | `DefaultBuiltInRegistry` (Core) | Register built-in JS objects |
| `IJSContext` | Runtime | `JSContext` (Core) | Execution context contract |
| `IJSFunction` | Runtime | Various (Core) | Callable JS function contract |
| `IJSPrototype` | Runtime | Various (Core) | Prototype chain contract |
| `IJSSymbol` | Runtime | `JSSymbol` (Core) | ES Symbol contract |
| `IDebugger` | Runtime | Implemented in Debugger | Breakpoint and stepping contract |
| `ICodeCache` | Runtime | Implemented in Core | Compiled code caching |
| `IJSModuleResolver` | Runtime | Implemented in Modules | Module resolution contract |
| `IJSDisposableStack` | Core | Implemented in BuiltIns | Disposable resource tracking |

### 3.2 Factory Delegates on `JSValue` (Runtime)

These delegates live on the `JSValue` class and are set by Core and BuiltIns
module initializers at startup.

| Delegate | Signature | Set By | Purpose |
|----------|-----------|--------|---------|
| `CreateNumber` | `Func<double, JSValue>` | Core | Create JS number values |
| `CreateString` | `Func<string, JSValue>` | Core | Create JS string values |
| `NewTypeError` | `Func<string, Exception>` | Core | Create TypeError exceptions |
| `ForceConvertHelper` | `Func<JSValue, object, bool, object>` | Core | Type coercion |
| `CreateDynamicMetaObject` | `Func<Expression, JSValue, DynamicMetaObject>` | Core | DLR interop |
| `NumberToECMAString` | `Func<double, string>` | Core | Number → string (ECMA spec) |
| `CreatePrototypeObject` | `Func<JSValue, IJSPrototype>` | Core | Prototype creation |
| `InvokePropertyGetter` | `Func<IPropertyAccessor, JSValue, JSValue>` | Core | Property accessor invocation |
| `CreateDecimalFactory` | `Func<decimal, JSValue>` | BuiltIns | Create `JSDecimal` |
| `CreateDecimalFromStringFactory` | `Func<string, JSValue>` | BuiltIns | Parse string → `JSDecimal` |
| `CreateBigIntFromStringFactory` | `Func<string, JSValue>` | BuiltIns | Parse string → `JSBigInt` |
| `CreateBigIntFactory` | `Func<long, JSValue>` | BuiltIns | Create `JSBigInt` |

### 3.3 Factory Delegates on `DefaultBuiltInRegistry` (Core)

| Delegate | Signature | Set By | Purpose |
|----------|-----------|--------|---------|
| `AdditionalRegistrations` | `Action<JSContext>` | BuiltIns | Register satellite built-in types into a context |
| `ConsoleFactory` | `Func<JSContext, object>` | BuiltIns | Create `JSConsole` without Core referencing it |
| `StructuredCloneExtension` | `Func<JSValue, Dictionary<JSValue,JSValue>, Func<…,JSValue>, JSValue>` | BuiltIns | Structured-clone support for `Map`, `Set`, `ArrayBuffer` |
| `IteratorPrototypeSetup` | `Action<JSObject>` | BuiltIns | Register `Iterator.prototype` helper methods |

### 3.4 Other Cross-Assembly Delegates

| Delegate / Static | Location | Set By | Purpose |
|--------------------|----------|--------|---------|
| `JSGlobalStatic.IntlFactory` | Core | BuiltIns | Create `Intl` global object |
| `JSDate.IntlDateFormatter` | Core | BuiltIns | Internationalized date formatting |
| `IJSDisposableStack.CreateNew` | Core | BuiltIns | Disposable stack factory |
| `JSContext.ClrInterop` | Core | Clr | Full CLR interop (replaces `FallbackClrInterop`) |
| `JSContext.ClrModuleProvider` | Core | Clr | CLR-based module provider |
| `DefaultJSCompiler.Register()` | Core | Compiler | Register `FastCompiler` factory |
| `ClrProxyBuilder.Register()` | Core | Clr | Register CLR proxy expression builders |

---

## 4. Module Initializers

Six `[ModuleInitializer]` methods fire when their containing assembly is first
loaded. They wire the delegates and registrations listed in §3.

| # | Assembly | Class | Key Registrations |
|---|----------|-------|-------------------|
| 1 | Core | `JSValueCoreExtensions` | `CreateNumber`, `CreateString`, `NewTypeError`, `ForceConvertHelper`, `CreateDynamicMetaObject`, `NumberToECMAString`, `CreatePrototypeObject`, `InvokePropertyGetter` |
| 2 | Core | `CoreScriptCoreExtensions` | Core script compilation wiring |
| 3 | Core | `PropertySequenceCoreExtensions` | Property sequence runtime wiring |
| 4 | BuiltIns | `BuiltInsAssemblyInitializer` | `AdditionalRegistrations`, `ConsoleFactory`, `StructuredCloneExtension`, `IteratorPrototypeSetup`, `CreateDecimalFactory`, `CreateBigIntFactory`, `IntlFactory`, `IntlDateFormatter`, `IJSDisposableStack.CreateNew` |
| 5 | Compiler | `CompilerAssemblyInitializer` | `DefaultJSCompiler.Register()` (FastCompiler factory) |
| 6 | Clr | `ClrAssemblyInitializer` | `JSContext.ClrInterop`, `ClrProxyBuilder.Register()`, `JSContext.ClrModuleProvider` |

### 4.1 Initialization Order

Module initializers fire lazily when the CLR first accesses a type in their
assembly. No guaranteed ordering exists across assemblies. The architecture
is designed so that each initializer is **idempotent** and **order-independent**:

- Core initializers (1–3) fire when any `JSValue` or `CoreScript` type is used.
- Satellite initializers (4–6) fire when their assembly is loaded — typically
  triggered by the `Broiler.JavaScript.All` meta-package or an explicit
  `RuntimeHelpers.RunClassConstructor` call in tests.

If a satellite assembly is never loaded, its delegates remain `null` and Core
falls back to default behaviour (see §5).

---

## 5. Fallback Behaviour

When optional satellite assemblies are not loaded, Core degrades gracefully.

| Satellite Not Loaded | Fallback Behaviour |
|----------------------|-------------------|
| **BuiltIns** | `AdditionalRegistrations` is `null` → only Core-registered globals available. `ConsoleFactory` is `null` → no console object. `CreateDecimalFactory` / `CreateBigIntFactory` are `null` → `JSDecimal` / `JSBigInt` literals throw at runtime. `StructuredCloneExtension` is `null` → structured clone skips Map/Set/ArrayBuffer. `IteratorPrototypeSetup` is `null` → `Iterator.prototype` has no helper methods. |
| **Compiler** | `DefaultJSCompiler` has no registered factory → `eval()` and dynamic compilation throw `NotSupportedException`. |
| **Clr** | `JSContext.ClrInterop` stays as `FallbackClrInterop` → primitive types (bool, int, double, string, DateTime, etc.) marshal correctly; complex .NET objects return `JSUndefined`. `ClrModuleProvider` is `null` → CLR module loading unavailable. |
| **Debugger** | No debugger hooks registered → breakpoints and step commands are no-ops. |
| **Modules** | `IJSModuleResolver` not set → `import` / `require` calls fail with module-not-found errors. |

---

## 6. Type Forwarding Summary

**75 `[assembly: TypeForwardedTo(…)]`** attributes in Core ensure backward
compatibility after types were moved to Foundation-layer assemblies.
Consumers that reference Core by assembly name continue to resolve these types
without recompilation.

| Forwarding File | Target Assembly | Types Forwarded (count) | Examples |
|-----------------|-----------------|------------------------|----------|
| `AssemblyInfo.cs` | Runtime | 35 | `JSValue`, `Arguments`, `KeyString`, `PropertySequence`, `JSFunctionDelegate`, `IJSPrototype`, `IClrInterop`, `IDebugger` |
| `ClrTypeForwarding.cs` | Runtime | 9 | `JSExportAttribute`, `ClrMemberNamingConvention`, `DictionaryCodeCache`, `MethodProvider`, `ListMethodProvider`, `IJavaScriptObject` |
| `ParserTypeForwarding.cs` | Parser | 12 | `FastParser`, `FastScanner`, `FastKeywordMap`, `FastTokenStream`, `FastScope`, `FastPool`, `FastList<>` |
| `StorageTypeForwarding.cs` | Storage | 10 | `VirtualMemory<>`, `StringMap<>`, `ConcurrentNameMap`, `SAUint32Map<>`, `ConcurrentStringMap<>` |
| `AstTypeForwarding.cs` | Ast | 14 | `AstNode`, `AstExpression`, `AstStatement`, `AstProgram`, `FastNodeType`, `StringSpan`, `SpanLocation` |

> **Important:** Type forwarding flows **downward only** (Core → Foundation).
> It is not used from Core → BuiltIns because that would create a circular
> reference (BuiltIns already references Core).

---

## 7. Circular Reference Prevention

### 7.1 Rules

1. **No upward references.** A lower-layer assembly must never reference a
   higher-layer assembly. The layering order is:
   Foundation → Core → Feature → Integration → Application.
2. **No peer Feature-to-Feature references** except the documented exceptions
   (ModuleExtensions → Clr + Modules; Network → BuiltIns + Clr).
3. **No type forwarding upward.** `TypeForwardedTo` may only point to an
   assembly that the forwarder already references.
4. **Factory delegates for upward communication.** When Core needs to call
   into a Feature assembly, it defines a delegate property. The Feature
   assembly sets the delegate via its module initializer.

### 7.2 Verification

The CI pipeline validates these rules implicitly — any circular reference
causes a build failure (`dotnet build` detects project-reference cycles).
Additionally, the integration test suite (`Broiler.JavaScript.Integration.Tests`)
includes validation tests that assert:

- All expected module initializers fire.
- All factory delegates are non-null after satellite assembly load.
- Type forwarding resolves correctly across assembly boundaries.

---

## 8. Test Project Dependencies

Each test project references the assembly it tests plus any assemblies needed
to set up a working JS context.

| Test Project | References |
|--------------|------------|
| **Storage.Tests** | Storage |
| **Ast.Tests** | Ast |
| **Parser.Tests** | Parser |
| **Runtime.Tests** | Runtime, Core |
| **Core.Tests** | Core, Compiler |
| **Compiler.Tests** | Compiler, Core |
| **BuiltIns.Tests** | BuiltIns, Core, Compiler, Clr |
| **Clr.Tests** | Clr, Core, Compiler |
| **Debugger.Tests** | Debugger, Core |
| **Modules.Tests** | Modules, Core, Compiler |
| **ModuleExtensions.Tests** | ModuleExtensions, Core, Modules, Compiler |
| **Integration.Tests** | Core, Compiler, BuiltIns, Clr, Modules, ModuleExtensions, Runtime, Parser, Ast, Storage |

> **Integration.Tests** references every production assembly to validate
> cross-assembly wiring, type forwarding, and end-to-end JS execution.

---

## 9. Phase 2 — Planned Dependency Changes

The [Phase 2 roadmap](../roadmap/javascript-engine-assembly-refactor.md#10-phase-2-deep-structural-refactoring)
proposes structural changes that may affect the dependency graph. This section
documents anticipated changes for planning purposes.

### 9.1 Code-Generation Builder Isolation (M9)

**Current state:** `LinqExpressions/` (33 files), `Emit/` (2), `CodeGen/` (2),
`LambdaGen/` (1), `TypeQuery/` (1), `FastParser/Compiler/` (1), and
`Debugger/` adapter (1) live in `Broiler.JavaScript.Core`.

**Proposed change (preferred):** Move these 49 files into the `Compiler`
assembly under a `Builders/` subdirectory. This adds no new project references
because Compiler already references Core.

**Alternative (if runtime callers found):** Create a new
`Broiler.JavaScript.CodeGen` assembly.

| Scenario | New Assembly? | Dependency Change |
|----------|---------------|-------------------|
| Move to Compiler | No | None |
| New CodeGen assembly | Yes | `Compiler → CodeGen → Core` (CodeGen is new, sits between) |

If a new assembly is created, update §1 matrix and §1.1 graph, and add
CodeGen to the `Broiler.JavaScript.All` meta-package.

### 9.2 Foundation Layer Dedup (M11)

**Current state:** `CancellableDisposableAction` exists in both Runtime and
Parser.

**Proposed change:** Keep the Runtime copy. Parser would either:

1. Add a project reference to Runtime (safe — Runtime is a peer foundation
   assembly; no cycle because Parser → Ast ← Runtime, but Parser does not
   currently reference Runtime directly), or
2. If adding the reference is undesirable, extract the utility to
   ExpressionCompiler (a common dependency that both Parser and Runtime
   already reference).

Either option eliminates the duplicate. Update §1 matrix if Parser gains a
new reference.

### 9.3 Types Remaining in Core After Phase 2

After M9 coupling analysis, the Core assembly retains its ~180 files because the
LinqExpressions builders could not be extracted due to runtime coupling (see
roadmap M9 analysis). The Core assembly contains:

- **Runtime objects:** JSContext, JSGlobal, JSObject, JSArray, JSString,
  JSNumber, JSBoolean, JSFunction, JSClass, JSSymbol, JSNull, JSUndefined
- **Non-extractable built-ins:** RegExp (compiler coupling), Promise
  (JSContext infrastructure)
- **Expression builders:** LinqExpressions/ (used at runtime by
  `JSFunction.CreateClrDelegate()` via ClrProxyBuilder/ArgumentsBuilder)
- **Code generation support:** CodeGen/, LambdaGen/, TypeQuery/, Emit/,
  FastParser/Compiler/, Debugger/ (various runtime dependencies)
- **Property/prototype infrastructure:** DefaultBuiltInRegistry, JSPrototype,
  scope management, property descriptors
- **Extensions & utilities:** Type-checking helpers, collection extensions,
  hash utilities
- **CLR fallback stubs:** FallbackClrInterop, MarshalExtensions

The original M9 goal of reducing Core by ~49 files (~22%) was not achievable
because the coupling analysis (M9 step 9.1) revealed runtime callers for the
builder types. See the roadmap's M9 section for detailed analysis.

---

## 10. Quick Reference — Adding a New Dependency

Before adding a new project or package reference, verify:

1. **Layer rule** — Does the reference flow downward? (§7 rule 1)
2. **Cycle check** — Run `dotnet build` to confirm no circular reference.
3. **Factory delegate** — If the new code in a Feature assembly must be called
   from Core, add a delegate property to `DefaultBuiltInRegistry` or `JSValue`
   and wire it in the Feature's module initializer. Do not add a direct
   project reference from Core to the Feature assembly.
4. **Type forwarding** — If moving a public type to a lower-layer assembly,
   add a `[assembly: TypeForwardedTo(typeof(…))]` in the original assembly.
5. **Tests** — Add or update tests in the relevant test project and in
   Integration.Tests if the change affects cross-assembly wiring.
6. **This document** — Update the dependency matrix (§1) and any affected
   sections when project references change.
