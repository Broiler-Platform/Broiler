# Module Initializer Chain & Startup Sequence

This document describes the module initializer chain that wires the
Broiler.JavaScript engine assemblies together at runtime. Understanding this
chain is essential for adding new satellite assemblies, debugging initialization
failures, and extending the engine.

---

## Overview

.NET module initializers (`[ModuleInitializer]`) run automatically when an
assembly is first loaded by the runtime. The Broiler.JavaScript engine uses
**6 module initializers** across 4 assemblies to register factory delegates,
wire interop, and set up the compilation pipeline — all without requiring
circular assembly references.

```
Assembly Load Order (lazy — triggered by first type access)
│
├─ Broiler.JavaScript.Core
│   ├─ JSValueCoreExtensions.InitializeFactories()
│   ├─ CoreScriptCoreExtensions.InitializeFactories()
│   └─ PropertySequenceCoreExtensions.InitializeTypeErrorFactory()
│
├─ Broiler.JavaScript.BuiltIns
│   └─ BuiltInsAssemblyInitializer.Initialize()
│
├─ Broiler.JavaScript.Compiler
│   └─ CompilerAssemblyInitializer.Initialize()
│
└─ Broiler.JavaScript.Clr
    └─ ClrAssemblyInitializer.Initialize()
```

> **Important:** .NET loads assemblies lazily. Module initializers run when the
> assembly is first accessed, not at application startup. The order above
> reflects typical load order but is not guaranteed. Each initializer is
> designed to be order-independent within its assembly.

---

## Module Initializers

### 1. `JSValueCoreExtensions.InitializeFactories()`

**Assembly:** `Broiler.JavaScript.Core`
**File:** `Broiler.JavaScript.Core/Core/JSValueCoreExtensions.cs`

Wires factory delegates on `JSValue` (which lives in the Runtime assembly) so
that Runtime types can create concrete JS values without referencing Core:

| Delegate / Property | Value |
|---------------------|-------|
| `JSValue.UndefinedValue` | `JSUndefined.Value` |
| `JSValue.NullValue` | `JSNull.Value` |
| `JSValue.BooleanTrue` | `JSBoolean.True` |
| `JSValue.BooleanFalse` | `JSBoolean.False` |
| `JSValue.NumberOne` | `JSNumber.One` |
| `JSValue.NumberNaN` | `JSNumber.NaN` |
| `JSValue.CreateNumber` | `v => new JSNumber(v)` |
| `JSValue.CreateString` | `v => new JSString(v)` |
| `JSValue.NewTypeError` | `msg => JSContext.NewTypeError(msg)` |
| `JSValue.NumberToECMAString` | `JSNumber.ToECMAString` |
| `JSValue.CreateDynamicMetaObject` | `(param, value) => new JSDynamicMetaData(param, value)` |
| `JSValue.ForceConvertHelper` | CLR unwrapping conversion |
| `JSValue.InvokePropertyGetter` | `(getter, receiver) => ((JSFunction)getter).InvokeFunction(...)` |
| `JSValue.CreatePrototypeObject` | `value => (value as JSObject)?.PrototypeObject` |
| `Arguments.Empty` | `new Arguments(JSUndefined.Value)` |
| `Arguments.ForApplyImpl` | `ArgumentsCoreExtensions.ForApplyCore` |
| `Arguments.RestFromImpl` | `ArgumentsCoreExtensions.RestFromCore` |
| `Arguments.GetStringImpl` | `ArgumentsCoreExtensions.GetStringCore` |
| `Arguments.GetSpreadTarget` | `ArgumentsCoreExtensions.GetSpreadTargetCore` |

### 2. `CoreScriptCoreExtensions.InitializeFactories()`

**Assembly:** `Broiler.JavaScript.Core`
**File:** `Broiler.JavaScript.Core/CoreScriptCoreExtensions.cs`

Wires `CoreScript` factory delegates so the Runtime-hosted `CoreScript` can
access Core-only types (`JSContext`, `DefaultJSCompiler`, `AsyncPump`, etc.):

| Delegate | Purpose |
|----------|---------|
| `CoreScript.CreateDefaultCompiler` | Creates a `DefaultJSCompiler` instance |
| `CoreScript.GetDefaultCodeCache` | Returns `DictionaryCodeCache.Current` |
| `CoreScript.GetCurrentContext` | Returns `JSContext.Current` and its code cache |
| `CoreScript.GetCurrentWaitTask` | Returns the current context's async wait task |
| `CoreScript.CreateSyntaxError` | Creates a `SyntaxError` via `JSContext.NewSyntaxError` |
| `CoreScript.RunAsyncPump` | Wraps `AsyncPump.Run` for async execution |

### 3. `PropertySequenceCoreExtensions.InitializeTypeErrorFactory()`

**Assembly:** `Broiler.JavaScript.Core`
**File:** `Broiler.JavaScript.Core/Core/Storage/PropertySequenceCoreExtensions.cs`

Wires the `PropertySequence.TypeErrorFactory` delegate so that property
deletion errors in the Storage assembly produce the correct JavaScript
`TypeError` exception:

```csharp
PropertySequence.TypeErrorFactory = msg => JSContext.NewTypeError(msg);
```

### 4. `BuiltInsAssemblyInitializer.Initialize()`

**Assembly:** `Broiler.JavaScript.BuiltIns`
**File:** `Broiler.JavaScript.BuiltIns/BuiltInsAssemblyInitializer.cs`

The largest initializer — registers all built-in types and wires factory
delegates:

| Registration | Purpose |
|-------------|---------|
| `DefaultBuiltInRegistry.AdditionalRegistrations` | Chains `context.RegisterBuiltInClasses()` for BuiltIns assembly |
| `IJSDisposableStack.CreateNew` | Factory for `JSDisposableStack` (Compiler creates instances via interface) |
| `JSGlobalStatic.IntlFactory` | Factory for `Intl` global object (avoids `JSGlobal` → `JSIntl` reference) |
| `JSDate.IntlDateFormatter` | Factory for Intl date formatting (avoids `JSDatePrototype` → `JSIntlDateTimeFormat`) |
| `JSValue.CreateDecimalFactory` | Factory for `JSDecimal` from `decimal` value |
| `JSValue.CreateDecimalFromStringFactory` | Factory for `JSDecimal` from string |
| `JSValue.CreateBigIntFromStringFactory` | Factory for `JSBigInt` from string |
| `JSValue.CreateBigIntFactory` | Factory for `JSBigInt` from `long` value |
| `DefaultBuiltInRegistry.ConsoleFactory` | Factory for `JSConsole` (avoids `DefaultBuiltInRegistry` → `JSConsole`) |
| `DefaultBuiltInRegistry.StructuredCloneExtension` | Clone handler for `JSMap`, `JSSet`, `JSArrayBuffer` |
| `DefaultBuiltInRegistry.IteratorPrototypeSetup` | Registers 11 iterator helper methods on `Iterator.prototype` |

### 5. `CompilerAssemblyInitializer.Initialize()`

**Assembly:** `Broiler.JavaScript.Compiler`
**File:** `Broiler.JavaScript.Compiler/CompilerAssemblyInitializer.cs`

Registers the `FastCompiler`-based compilation pipeline:

```csharp
DefaultJSCompiler.Register(
    (code, location, argsList, codeCache) =>
    {
        var compiler = new FastCompiler(code, location, argsList, codeCache);
        return compiler.Method;
    });
```

> **Eager loading:** `DefaultJSCompiler` proactively loads the Compiler
> assembly in its static constructor to guarantee this initializer runs before
> the first compilation attempt.

### 6. `ClrAssemblyInitializer.Initialize()`

**Assembly:** `Broiler.JavaScript.Clr`
**File:** `Broiler.JavaScript.Clr/ClrAssemblyInitializer.cs`

Wires CLR/.NET interop infrastructure:

| Registration | Purpose |
|-------------|---------|
| `JSContext.ClrInterop` | Sets `DefaultClrInterop.Instance` (full CLR interop implementation) |
| `ClrProxyBuilder.Register(...)` | Expression tree builder for CLR proxy marshalling (`ClrExpressionBuilder.Marshal`, `ClrExpressionBuilder.From`) |
| `JSContext.ClrModuleProvider` | Sets `ClrModule.Default` as the default CLR module provider |

---

## Runtime Startup Sequence

When a `JSContext` is created, the following sequence occurs:

```
1. Application code references a type from Broiler.JavaScript.Core
   └─ .NET loads Core assembly
      ├─ JSValueCoreExtensions.InitializeFactories()
      ├─ CoreScriptCoreExtensions.InitializeFactories()
      └─ PropertySequenceCoreExtensions.InitializeTypeErrorFactory()

2. Application references Broiler.JavaScript.BuiltIns (or All)
   └─ .NET loads BuiltIns assembly
      └─ BuiltInsAssemblyInitializer.Initialize()
         ├─ Chains AdditionalRegistrations
         ├─ Wires factory delegates (Decimal, BigInt, Console, Intl, etc.)
         ├─ Wires StructuredCloneExtension
         └─ Wires IteratorPrototypeSetup

3. Application references Broiler.JavaScript.Compiler
   └─ .NET loads Compiler assembly
      └─ CompilerAssemblyInitializer.Initialize()
         └─ Registers FastCompiler pipeline

4. Application references Broiler.JavaScript.Clr
   └─ .NET loads Clr assembly
      └─ ClrAssemblyInitializer.Initialize()
         ├─ Sets DefaultClrInterop
         ├─ Registers ClrProxyBuilder
         └─ Sets ClrModuleProvider

5. JSContext.CreateNew() is called
   └─ DefaultBuiltInRegistry.Register(context)
      ├─ context.RegisterGeneratedClasses()      ← Core's generated classes
      ├─ AdditionalRegistrations(context)        ← BuiltIns' generated classes
      ├─ SetupIteratorPrototypeChain(context)
      │   ├─ Iterator.prototype[Symbol.iterator] = this
      │   ├─ IteratorPrototypeSetup(proto)       ← 11 helper methods
      │   └─ Generator.prototype → Iterator.prototype
      └─ ConsoleFactory(context)                 ← JSConsole object
```

---

## Troubleshooting

### Delegate is `null` at runtime

**Symptom:** `NullReferenceException` when Core calls a factory delegate.

**Cause:** The satellite assembly's module initializer has not run yet.

**Fix:** Ensure the satellite assembly is loaded before the delegate is used.
Force loading with:

```csharp
RuntimeHelpers.RunClassConstructor(typeof(SomeTypeInSatelliteAssembly).TypeHandle);
```

### Initialization order matters

Module initializers within the **same assembly** run in an unspecified order.
If two initializers in the same assembly depend on each other, combine them
into a single initializer or use explicit ordering via static constructor
chains.

Initializers in **different assemblies** run when the assembly is loaded.
Since assemblies load lazily, ensure transitive references or explicit loading
for assemblies that must initialize before use.

### Adding a new module initializer

1. Create a new `internal static class` with a `[ModuleInitializer]` method.
2. Wire any delegates or registrations needed.
3. Document the initializer in this file.
4. Add a test that verifies the delegate is non-null after initialization.

---

## References

- [Extraction Pattern Guide](extraction-pattern.md)
- [Contributing: Adding New Built-In Types](contributing-builtins.md)
- [Assembly Refactor Roadmap](../roadmap/javascript-engine-assembly-refactor.md)
