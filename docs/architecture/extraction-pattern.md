# Built-In Type Extraction Pattern

This document describes the repeatable pattern used to extract built-in
JavaScript types from `Broiler.JavaScript.Core` into `Broiler.JavaScript.BuiltIns`.
The pattern was established during milestones M2–M4 and refined through M7.

---

## Overview

Extraction moves a built-in type **out of Core** and into the **BuiltIns**
assembly while preserving binary and source compatibility for existing consumers.
Three mechanisms work together:

1. **Move the type** — relocate the `.cs` file(s) from `Core/` to `BuiltIns/`,
   keeping the original namespace.
2. **Add type forwarding** *(Foundation extractions only)* — add a
   `[TypeForwardedTo]` attribute in Core so that code compiled against Core can
   still resolve the type.
3. **Wire a factory delegate** *(if Core instantiates the type)* — define a
   `static` delegate property in Core, set it in `BuiltInsAssemblyInitializer`,
   and call the delegate instead of the concrete constructor.

> **Note:** Type forwarding from Core → BuiltIns is **not possible** because
> BuiltIns already references Core — adding a forwarding in Core would create a
> circular reference. Namespace preservation alone provides source compatibility
> for Core → BuiltIns extractions.

---

## Step-by-Step Checklist

### 1. Coupling Analysis

Before extracting a type, verify that it has no hard coupling to:

| Dependency | Problem | Resolution |
|------------|---------|------------|
| **Compiler** (`LinqExpressions/`) | Compiler emits expression trees that reference the type | Introduce a factory delegate (see JSBigInt example) |
| **Parser** | Parser directly references the type | Usually acceptable if limited to validation |
| **Core infrastructure** (`JSContext` fields, prototype chains) | Core stores or creates instances | Introduce a factory delegate or registration delegate |

Run a text search for the type name across Core and Compiler assemblies:

```bash
grep -rn "TypeName" Broiler.JavaScript.Core/ Broiler.JavaScript.Compiler/
```

### 2. Move the Type

1. Move the `.cs` file(s) from `Broiler.JavaScript.Core/Core/<Subdirectory>/`
   to `Broiler.JavaScript.BuiltIns/<Subdirectory>/`.
2. **Keep the original namespace** — do not change the `namespace` declaration.
   This ensures existing `using` directives continue to work.
3. Update the `.csproj` if needed (usually automatic with SDK-style projects).

### 3. Add Type Forwarding (Foundation Extractions Only)

If the type moved from Core to a **Foundation** assembly (Runtime, Storage,
Ast, Parser), add to the appropriate forwarding file in Core:

```csharp
[assembly: TypeForwardedTo(typeof(Namespace.TypeName))]
```

Forwarding files:
- `ClrTypeForwarding.cs` — Runtime types (9 types: attributes, caching, interop)
- `ParserTypeForwarding.cs` — Parser types (12 types)
- `StorageTypeForwarding.cs` — Storage types (10 types)

### 4. Wire Factory Delegate (If Core Instantiates the Type)

If Core needs to create instances of the extracted type:

**a) Define the delegate in Core:**

```csharp
// In the appropriate Core class (JSValue, DefaultBuiltInRegistry, etc.)
public static Func<string, JSValue> CreateExampleFactory { get; set; }
```

**b) Set the delegate in `BuiltInsAssemblyInitializer.Initialize()`:**

```csharp
// In BuiltInsAssemblyInitializer.cs
JSValue.CreateExampleFactory = static s => new JSExample(s);
```

**c) Replace direct construction in Core:**

```csharp
// Before (in Core):
return new JSExample(value);

// After (in Core):
return JSValue.CreateExampleFactory(value);
```

### 5. Register via Source Generator

If the type uses `[JSClassGenerator]` or `[JSFunctionGenerator]`, it is
registered automatically when the BuiltIns assembly calls
`context.RegisterBuiltInClasses()` in its module initializer.

### 6. Update `BuiltInsAssemblyInitializer`

Ensure the module initializer chains correctly:

```csharp
var existing = DefaultBuiltInRegistry.AdditionalRegistrations;
DefaultBuiltInRegistry.AdditionalRegistrations = existing == null
    ? static context => context.RegisterBuiltInClasses()
    : context =>
    {
        existing(context);
        context.RegisterBuiltInClasses();
    };
```

### 7. Add Tests

- Add extraction validation tests in `Broiler.JavaScript.Integration.Tests/`
  verifying the type's assembly location, delegate wiring, and functional
  behavior.
- Run the full test suite: `dotnet test YantraJS.sln`

### 8. Verify

Run the full validation checklist:

- [ ] Type resolves from the expected assembly (`typeof(T).Assembly.GetName().Name`)
- [ ] No circular assembly references
- [ ] All factory delegates are non-null after initialization
- [ ] All existing tests pass
- [ ] CI passes on all 3 platforms (ubuntu, windows, macos)

---

## Examples

### Example 1: Simple Type (No Core Instantiation) — JSMath

`JSMath` is a pure built-in object with no Core-side instantiation. Extraction
required only moving the file and letting the source generator handle
registration.

**Steps taken:**
1. Moved `Core/Core/Math/JSMath.cs` → `BuiltIns/Math/JSMath.cs`
2. Namespace `Broiler.JavaScript.Core.Core.Math` preserved
3. No type forwarding needed (Core → BuiltIns extraction)
4. No factory delegate needed (Core never creates `new JSMath()`)
5. `[JSClassGenerator]` on `JSMath` → auto-registered via
   `context.RegisterBuiltInClasses()` in `BuiltInsAssemblyInitializer`

### Example 2: Type with Factory Delegate — JSBigInt

`JSBigInt` is referenced by the Compiler via `JSBigIntBuilder` expression
trees. A factory delegate was introduced to decouple the dependency.

**Steps taken:**
1. Moved `Core/Core/BigInt/JSBigInt.cs` → `BuiltIns/BigInt/JSBigInt.cs`
2. Namespace `Broiler.JavaScript.Core.Core.BigInt` preserved
3. Added factory delegates on `JSValue`:
   ```csharp
   public static Func<string, JSValue> CreateBigIntFromStringFactory { get; set; }
   public static Func<long, JSValue> CreateBigIntFactory { get; set; }
   ```
4. Wired in `BuiltInsAssemblyInitializer`:
   ```csharp
   JSValue.CreateBigIntFromStringFactory = static s => new JSBigInt(s);
   JSValue.CreateBigIntFactory = static v => new JSBigInt(v);
   ```
5. Updated `JSBigIntBuilder` in Core to use `Expression.Call` against the
   factory delegate instead of `new JSBigInt(...)` constructor.
6. Updated `JSGlobal.TypeOf()` check to use the delegate.

### Example 3: Type with Registration Delegate — JSIteratorObject

`JSIteratorObject` provides iterator helper methods (map, filter, take, etc.)
that must be registered on `Iterator.prototype`. A registration delegate was
introduced so Core's `DefaultBuiltInRegistry` can set up the prototype chain
without referencing the concrete type.

**Steps taken:**
1. Moved `Core/Core/Iterator/JSIteratorObject.cs` →
   `BuiltIns/Iterator/JSIteratorObject.cs`
2. Namespace `Broiler.JavaScript.Core.Core.Iterator` preserved
3. Added `IteratorPrototypeSetup` delegate on `DefaultBuiltInRegistry`:
   ```csharp
   public static Action<JSObject> IteratorPrototypeSetup { get; set; }
   ```
4. Made `AddProto` method public on `DefaultBuiltInRegistry` for satellite
   assembly use.
5. Wired 11 iterator helper methods in `BuiltInsAssemblyInitializer`:
   ```csharp
   DefaultBuiltInRegistry.IteratorPrototypeSetup = static proto =>
   {
       DefaultBuiltInRegistry.AddProto(proto, "map", JSIteratorObject.StaticMap);
       DefaultBuiltInRegistry.AddProto(proto, "filter", JSIteratorObject.StaticFilter);
       // ... 9 more helper methods
   };
   ```
6. Replaced `Names.Iterator` (source-generated) with
   `KeyStrings.GetOrCreate("Iterator")` in `DefaultBuiltInRegistry`.

---

## Decision Tree: Extract vs. Keep in Core

```
Is the type referenced by the Compiler's expression-tree builders?
├─ YES → Can the reference be replaced with a factory delegate or Initialize(Type) pattern?
│        ├─ YES → Extract (follow JSBigInt or JSRegExp pattern)
│        └─ NO  → Keep in Core
└─ NO  → Is the type stored as a field on JSContext or a Core infrastructure class?
         ├─ YES → Can the field be replaced with an interface or delegate?
         │        ├─ YES → Extract (follow JSConsole/JSDisposableStack pattern)
         │        └─ NO  → Keep in Core (e.g., Promise — JSContext.PendingPromises)
         └─ NO  → Extract (follow JSMath pattern — simplest case)
```

---

## Non-Extractable Types

The following types were analyzed and determined to be non-extractable:

| Type | Reason |
|------|--------|
| **Promise** | `JSContext.PendingPromises` field is `ConcurrentDictionary<long, JSPromise>`; `JSAsyncFunction` creates instances directly — infrastructure-level coupling |

## Successfully Extracted Types (formerly non-extractable)

| Type | Approach | Notes |
|------|----------|-------|
| **RegExp** | `Initialize(Type)` pattern for `JSRegExpBuilder`; `IJSRegExp` interface in Runtime; `is IJSRegExp` for assemblies without BuiltIns reference | Previously blocked by `JSRegExpBuilder` compile-time coupling; resolved by switching to runtime `Initialize(Type)` pattern (same as `JSArrayBuilder`, `JSStringBuilder`) |

---

## References

- [Assembly Refactor Roadmap](../roadmap/javascript-engine-assembly-refactor.md)
- [Module Initializer Chain](module-initializers.md)
- [Contributing: Adding New Built-In Types](contributing-builtins.md)

---

## Appendix: Intra-Assembly File Splitting (M10 Pattern)

When a single file within an assembly grows beyond ~300 lines, it should be
split into partial files grouped by semantic theme. This pattern was used in M10
to decompose 5 oversized Core files into 16 partial files.

### Splitting Rules

1. **Create a partial class per theme.** Name the file
   `{ClassName}.{Theme}.cs` (e.g., `JSArray.Iteration.cs`).
2. **Keep fields and constructors in the primary file.** The primary file
   (`{ClassName}.cs`) retains all fields, properties, constructors, and any
   initialization logic.
3. **Partial files contain only methods.** Group related methods by
   functionality (iteration, search, modification, formatting, etc.).
4. **No namespace changes.** All partial files use the same namespace as the
   primary file.
5. **No public API changes.** The split is purely structural; consumers see
   no difference.

### Naming Conventions

| File | Contents |
|------|----------|
| `JSArray.cs` | Fields, constructor, main file |
| `JSArrayPrototype.Iteration.cs` | map, filter, reduce, forEach, every, some, find, findIndex |
| `JSArrayPrototype.Search.cs` | indexOf, lastIndexOf, includes |
| `JSArrayPrototype.Modification.cs` | push, pop, shift, unshift, splice, fill, copyWithin |
| `JSArrayPrototype.Utility.cs` | concat, join, reverse, sort, slice, flat, flatMap, at |

### When to Split

- File exceeds ~300 lines
- File contains logically distinct groups of methods (e.g., getters vs setters)
- Multiple contributors frequently modify different sections
