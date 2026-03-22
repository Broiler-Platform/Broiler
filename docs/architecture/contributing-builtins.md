# Contributing: Adding New Built-In Types

This guide walks through the process of adding a new built-in JavaScript type
to the `Broiler.JavaScript.BuiltIns` assembly. Follow these steps to ensure
your type integrates correctly with the engine's architecture.

---

## Prerequisites

Before starting, read:
- [Extraction Pattern](extraction-pattern.md) — the general pattern for moving
  types between assemblies
- [Module Initializers](module-initializers.md) — how satellite assemblies
  register with Core

---

## Adding a New Built-In Type

### Step 1: Create the Type

Create a new `.cs` file in `Broiler.JavaScript.BuiltIns/` under an appropriate
subdirectory. Use the namespace `Broiler.JavaScript.Core.Core.<Subdirectory>`.

```csharp
using Broiler.JavaScript.Core.Core;
using Broiler.JavaScript.Core.Core.Function;

namespace Broiler.JavaScript.Core.Core.Example;

[JSClassGenerator("Example")]
public partial class JSExample : JSObject
{
    public JSExample() : base()
    {
    }
}
```

> **Namespace convention:** Built-in types use `Broiler.JavaScript.Core.Core.*`
> namespaces even when they live in the BuiltIns assembly. This preserves
> source compatibility and follows the established extraction pattern.

### Step 2: Use Source Generator Attributes

Decorate the class with the appropriate source generator attribute:

| Attribute | Use Case |
|-----------|----------|
| `[JSClassGenerator("Name")]` | Standard built-in class (e.g., `Map`, `Set`, `Date`) |
| `[JSFunctionGenerator("Name")]` | Function constructor (less common) |

The source generator creates registration code that is called automatically
by `context.RegisterBuiltInClasses()` in the module initializer.

### Step 3: Add Prototype Methods

Define JavaScript-accessible methods using `[JSExport]` or by manually adding
them to the prototype:

```csharp
[JSExport("toString")]
public JSValue ToStringJS(in Arguments a)
{
    return new JSString("[object Example]");
}
```

### Step 4: Wire Factory Delegate (If Needed)

If Core needs to create instances of the new type (e.g., for a global
function, structured clone, or compiler integration):

**a) Define the delegate in the appropriate Core class:**

```csharp
// In Broiler.JavaScript.Core — e.g., DefaultBuiltInRegistry.cs or JSValue.cs
public static Func<JSValue> CreateExampleFactory { get; set; }
```

**b) Set the delegate in `BuiltInsAssemblyInitializer.Initialize()`:**

```csharp
// In Broiler.JavaScript.BuiltIns/BuiltInsAssemblyInitializer.cs
SomeCore.CreateExampleFactory = static () => new JSExample();
```

### Step 5: Add Tests

Create tests in the appropriate test project:

- **Unit tests** in `Broiler.JavaScript.BuiltIns.Tests/` — test the type's
  JavaScript behavior in isolation.
- **Integration tests** in `Broiler.JavaScript.Integration.Tests/` — verify
  assembly location and factory delegate wiring.

Example unit test:

```csharp
[Fact]
public void Example_Constructor_Works()
{
    using var context = new JSContext();
    var result = context.Eval("new Example()");
    Assert.NotNull(result);
}
```

Example integration test:

```csharp
[Fact]
public void Example_InBuiltInsAssembly()
{
    var asm = typeof(JSExample).Assembly.GetName().Name;
    Assert.Equal("Broiler.JavaScript.BuiltIns", asm);
}
```

### Step 6: Run the Full Test Suite

```bash
cd Broiler.JavaScript
dotnet build YantraJS.sln
dotnet test YantraJS.sln
```

All existing tests must continue to pass. CI runs on 3 platforms (ubuntu,
windows, macos).

---

## Extracting an Existing Type from Core

If you are moving an existing type from `Broiler.JavaScript.Core` to
`Broiler.JavaScript.BuiltIns`, follow the [Extraction Pattern](extraction-pattern.md).

Key additional steps for extraction:

1. **Coupling analysis** — search for references to the type in Core and
   Compiler assemblies.
2. **Namespace preservation** — keep the original `namespace` declaration.
3. **Factory delegate** — if Core instantiates the type, introduce a delegate.
4. **Structured clone** — if the type participates in `structuredClone()`,
   extend `DefaultBuiltInRegistry.StructuredCloneExtension`.

---

## Common Patterns

### Global Object Registration

To register a new type as a global (e.g., `globalThis.Example`), add it in
`BuiltInsAssemblyInitializer`:

```csharp
JSGlobalStatic.ExampleFactory = static () =>
    JSContext.ClrInterop.GetClrType(typeof(JSExample));
```

### Prototype Chain Setup

To add methods to a shared prototype (like Iterator.prototype), use the
`AddProto` helper:

```csharp
DefaultBuiltInRegistry.AddProto(proto, "methodName", JSExample.StaticMethod);
```

### Structured Clone Support

To support `structuredClone()` for the new type, extend the
`StructuredCloneExtension` delegate in `BuiltInsAssemblyInitializer`:

```csharp
if (value is JSExample example)
{
    var clone = new JSExample(/* copy state */);
    seen[value] = clone;
    return clone;
}
```

---

## Checklist

Before submitting a PR for a new built-in type:

- [ ] Type is in `Broiler.JavaScript.BuiltIns/` with correct namespace
- [ ] Source generator attribute (`[JSClassGenerator]` or `[JSFunctionGenerator]`) applied
- [ ] Factory delegate wired if Core needs to instantiate the type
- [ ] Unit tests added in `Broiler.JavaScript.BuiltIns.Tests/`
- [ ] Integration tests added in `Broiler.JavaScript.Integration.Tests/`
- [ ] Full test suite passes (`dotnet test YantraJS.sln`)
- [ ] No circular assembly references introduced
- [ ] Documentation updated if the type adds a new decoupling pattern

---

## References

- [Extraction Pattern](extraction-pattern.md)
- [Module Initializers](module-initializers.md)
- [Assembly Refactor Roadmap](../roadmap/javascript-engine-assembly-refactor.md)
