# YantraJS Subsystem Map and Coupling Analysis

This document provides a comprehensive mapping of the subsystems in `YantraJS.Core`
and an analysis of coupling between them, as required by Milestone 1 of the
[Refactoring Roadmap](REFACTORING_ROADMAP.md).

---

## Subsystem Inventory

### 1. Parser (`FastParser/`)

| Component | Location | File Count | Description |
|-----------|----------|-----------|-------------|
| Lexer | `FastScanner.cs`, `FastToken.cs`, `FastTokenStream.cs` | 3 | Tokenizes JavaScript source into a token stream |
| Keywords | `FastKeywords.cs`, `FastKeywordMap.cs` | 2 | Keyword recognition and mapping |
| Token types | `TokenTypes.cs`, `FastNodeType.cs` | 2 | Token and AST node type enums |
| Parser core | `Parser/FastParser*.cs` | 30+ | Recursive-descent parser (partial class) |
| AST nodes | `Ast/*.cs` | 49 | Abstract syntax tree node definitions |
| Scope tracking | `Parser/FastScope.cs`, `Parser/FastScopeItem.cs` | 2 | Variable scope management during parsing |
| Memory pool | `Parser/FastPool.cs`, `Parser/FastList.cs`, `Parser/FastStack.cs` | 3 | Object pooling for allocation efficiency |
| Utilities | `ArraySpan.cs`, `CharExtensions.cs`, `SpanLocation.cs` | 3 | Supporting types |

**Public API surface:**
- `FastParser.ParseProgram()` → `AstProgram`
- `FastTokenStream` (constructor takes `StringSpan`)
- AST node types (read-only data structures)

**Dependencies:** `StringSpan` (from `YantraJS.Core` root)

---

### 2. Compiler (`FastParser/Compiler/`, `LinqExpressions/`)

| Component | Location | File Count | Description |
|-----------|----------|-----------|-------------|
| AST compiler | `FastParser/Compiler/FastCompiler*.cs` | 30+ | AST → LINQ Expression Trees |
| Expression builders | `LinqExpressions/JS*Builder.cs` | 40+ | Typed expression tree construction |
| Generator compilation | `LinqExpressions/GeneratorsV2/` | 12 | Generator function rewriting |

**Public API surface:**
- `FastCompiler` constructor (takes `StringSpan` code)
- `FastCompiler.Method` → compiled `JSFunctionDelegate`

**Dependencies:** Parser (AST nodes), Runtime (`JSValue`, `JSContext`, `Arguments`)

---

### 3. Runtime (`Core/` top-level)

| Component | Location | File Count | Description |
|-----------|----------|-----------|-------------|
| Context | `Core/JSContext.cs` | 1 (partial) | JavaScript execution context |
| Values | `Core/JSValue.cs`, `Core/JSVariable.cs` | 2 | Base value type and variable wrapper |
| Arguments | `Core/Arguments.cs` | 1 | Function invocation arguments |
| Bootstrap | `Core/Bootstrap.cs` | 1 | Context initialization and prototype setup |
| Primitives | `Core/Primitive/` | 4 | `JSNull`, `JSUndefined`, `JSPrimitive`, `JSPrimitiveObject` |
| Exceptions | `Core/JSException.cs` | 1 | JavaScript exception wrapper |
| Call stack | `Core/CallStackItem.cs` | 1 | Stack frame tracking |
| Key strings | `Core/KeyStrings.cs`, `Core/KeyString.cs` | 2 | Interned property name keys |
| Script entry | `CoreScript.cs` | 1 | High-level compile-and-evaluate bridge |

**Public API surface:**
- `JSContext` (creation, `Eval()`, `Execute()`, prototype access)
- `JSValue` (type checking, coercion, property access)
- `Arguments` (function parameter passing)
- `CoreScript.Compile()`, `CoreScript.Evaluate()`

**Dependencies:** Parser/Compiler (for code compilation), Storage (for property maps)

---

### 4. Built-in Objects (`Core/{Type}/`)

| Component | Location | File Count | Description |
|-----------|----------|-----------|-------------|
| Array | `Core/Array/` | 5 + `Typed/` (13) | Array and TypedArray implementations |
| BigInt | `Core/BigInt/` | 1 | BigInt type |
| Boolean | `Core/Boolean/` | 1 | Boolean wrapper |
| Date | `Core/Date/` | 3 | Date type and prototype |
| Error | `Core/Error/` | multiple | Error types (TypeError, SyntaxError, etc.) |
| Function | `Core/Function/` | multiple | Function type and prototype |
| Generator | `Core/Generator/` | 5 | Generator/iterator protocol |
| JSON | `Core/Json/` | multiple | JSON parse/stringify |
| Map/WeakMap | `Core/Map/` | multiple | Map and WeakMap types |
| Math | `Core/Objects/JSMath.cs` | 1 | Math built-in |
| Number | `Core/Number/` | multiple | Number type and prototype |
| Object | `Core/Object/` | 3 | Object type and prototype |
| Promise | `Core/Promise/` | multiple | Promise implementation |
| Proxy | `Core/Proxy/` | multiple | Proxy and handler |
| Reflect | `Core/Reflect/`, `Core/Objects/JSReflect.cs` | multiple | Reflect API |
| RegExp | `Core/RegExp/` | 2 | Regular expression type |
| Set/WeakSet | `Core/Set/` | 2 | Set and WeakSet types |
| String | `Core/String/` | multiple | String type and prototype |
| Symbol | `Core/Symbol/` | multiple | Symbol type |
| WeakRef | `Core/Weak/` | 1 | WeakRef type |
| Events | `Core/Events/` | 4 | EventTarget, CustomEvent |
| Global | `Core/Global/` | multiple | Global functions (eval, parseInt, etc.) |
| Intl | `Core/Intl/` | multiple | Internationalization API |

**Dependencies:** Runtime (`JSValue`, `JSContext`, `JSPrototype`, `Arguments`)

---

### 5. CLR Interop (`Core/Clr/`)

| Component | Location | File Count | Description |
|-----------|----------|-----------|-------------|
| Proxy | `ClrProxy.cs` | 1 | .NET object wrapper in JS |
| Type bridge | `ClrType.cs`, `ClrTypeBuilder.cs` | 2 | .NET type reflection and bridging |
| Module | `ClrModule.cs` | 1 | .NET assembly as JS module |
| Member info | `JSFieldInfo.cs`, `JSMethodInfo.cs`, `JSPropertyInfo.cs` | 3 | .NET member wrappers |
| Naming | `ClrMemberNamingConvention.cs` | 1 | camelCase/PascalCase conversion |
| Export | `JSExportAttribute.cs` | 1 | Attribute for exported members |

**Dependencies:** Runtime (`JSValue`, `JSContext`, `JSObject`)

---

### 6. Module System (`Core/Module/`)

| Component | Location | File Count | Description |
|-----------|----------|-----------|-------------|
| Module loader | `Core/Module/` | multiple | ES module resolution and loading |

**Dependencies:** Parser (for `import`/`export` parsing), Runtime (`JSContext`), CLR Interop (for `ClrModule`)

---

### 7. Debugger (`Debugger/`)

| Component | Location | File Count | Description |
|-----------|----------|-----------|-------------|
| V8 inspector | `Debugger/` | 27 | Chrome DevTools protocol implementation |
| Debug support | `Core/Debug/` | multiple | Debug hooks in runtime |

**Dependencies:** Runtime (`JSContext`, call stack), Compiler (source maps)

---

### 8. Storage (`Core/Storage/`)

| Component | Location | File Count | Description |
|-----------|----------|-----------|-------------|
| Property storage | `Core/Storage/` | 18 | Internal hash maps and property storage |

**Dependencies:** Runtime (`JSValue`, `KeyString`)

---

### 9. IL Emission (`Emit/`)

| Component | Location | File Count | Description |
|-----------|----------|-----------|-------------|
| IL helpers | `Emit/` | multiple | Low-level IL generation utilities |

**Dependencies:** `System.Reflection.Emit`

---

### 10. Code Generation (`CodeGen/`, `LambdaGen/`)

| Component | Location | File Count | Description |
|-----------|----------|-----------|-------------|
| Code gen | `CodeGen/` | multiple | Method generation utilities |
| Lambda gen | `LambdaGen/` | multiple | Lambda compilation helpers |

**Dependencies:** IL Emission, Runtime

---

## Coupling Analysis

### Dependency Graph

```
                    ┌──────────┐
                    │  Parser  │
                    │(FastParser)│
                    └────┬─────┘
                         │ produces AST
                         ▼
                    ┌──────────┐
                    │ Compiler │
                    │(FastCompiler│
                    │+ LinqExpr) │
                    └────┬─────┘
                         │ produces IL delegates
                         ▼
    ┌─────────┐    ┌──────────┐    ┌──────────┐
    │ Storage │◄───│ Runtime  │───►│ IL Emit  │
    └─────────┘    │(JSContext,│    └──────────┘
                   │ JSValue)  │
                   └──┬──┬──┬─┘
                      │  │  │
           ┌──────────┘  │  └──────────┐
           ▼             ▼             ▼
    ┌──────────┐  ┌──────────┐  ┌──────────┐
    │ Built-in │  │CLR Interop│  │ Debugger │
    │ Objects  │  └──────────┘  └──────────┘
    └──────────┘
           │
           ▼
    ┌──────────┐
    │  Module  │
    │  System  │
    └──────────┘
```

### Coupling Ratings

| From → To | Coupling | Notes |
|-----------|----------|-------|
| Parser → Runtime | **None** | Parser depends only on `StringSpan`; no runtime types |
| Compiler → Parser | **Low** | Consumes AST nodes (read-only data structures) |
| Compiler → Runtime | **High** | Expression builders reference `JSValue`, `JSContext`, `Arguments`, all built-in types |
| Runtime → Compiler | **Low** | `CoreScript.Compile()` instantiates `FastCompiler` |
| Built-ins → Runtime | **High** | Every built-in inherits from `JSValue`/`JSObject` and uses `JSContext` |
| Built-ins → Built-ins | **Low** | Minimal cross-references (e.g., Array uses Number for indices) |
| CLR Interop → Runtime | **Medium** | Wraps .NET types as `JSValue` subtypes |
| Module → Parser | **Low** | Uses parser for module source compilation |
| Module → Runtime | **Medium** | Module loading creates `JSContext` scopes |
| Module → CLR Interop | **Low** | `ClrModule` is one module type |
| Debugger → Runtime | **Medium** | Hooks into `JSContext` and call stack |
| Storage → Runtime | **Medium** | Provides property maps used by `JSObject` |
| IL Emit → None | **None** | Self-contained, depends only on `System.Reflection.Emit` |

### Extraction Priority (least coupled first)

1. **Parser** — Self-contained; depends only on `StringSpan`. Easiest to extract.
2. **IL Emission** — Self-contained; depends only on BCL reflection types.
3. **Storage** — Depends on `KeyString` and `JSValue` interfaces; can be isolated with interfaces.
4. **Compiler** — Depends on Parser AST and Runtime types; requires interface abstraction.
5. **CLR Interop** — Depends on Runtime; can be isolated behind interfaces.
6. **Debugger** — Depends on Runtime; largely independent from other subsystems.
7. **Built-in Objects** — Tightly coupled to Runtime; extract after Runtime interfaces are stable.
8. **Module System** — Depends on Parser, Runtime, and CLR Interop; extract last.

### Key Coupling Hotspots

1. **`JSContext`** — Central coupling point. Referenced by nearly every subsystem. Must be split into focused interfaces before module extraction.
2. **`JSValue` hierarchy** — Base type for all JS values. Built-ins, CLR interop, and compiler all depend on it.
3. **`Arguments`** — Used by every function invocation path. Shared across compiler, runtime, and built-ins.
4. **`CoreScript`** — Bridges Parser/Compiler into Runtime. Clean separation point.
5. **`Bootstrap`** — Registers all built-in types on `JSContext`. Must be made extensible.

---

## Recommended Interface Boundaries

| Boundary | Proposed Interface | Purpose |
|----------|-------------------|---------|
| Parser output | `AstNode` hierarchy (already exists) | Clean data contract between parser and compiler |
| Compilation | `IJSCompiler` | Abstract compilation so alternative backends can be swapped |
| Context | `IJSContext` | Decouple built-ins from concrete `JSContext` |
| Value factory | `IJSValueFactory` | Create `JSNumber`, `JSString`, etc. without direct coupling |
| Module resolution | `IJSModuleResolver` | Already partially exists; formalize |
| Debugger | `IJSDebugger` | Already partially exists via `JSDebugger` class |

---

*This analysis supports the subsystem mapping and coupling analysis deliverable of Milestone 1.
See [REFACTORING_ROADMAP.md](REFACTORING_ROADMAP.md) for the full refactoring plan.*
