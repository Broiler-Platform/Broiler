# Roadmap for Refactoring YantraJS JavaScript Engine

This document establishes a clear roadmap for refactoring the `yantrajs` JavaScript engine
(`yantra-1.2.295/`) as used within the Broiler project.

---

## Current Architecture

YantraJS follows a multi-layered compilation pipeline:

```
JavaScript source
    ↓
FastParser (lexer + parser) → AST
    ↓
LinqExpressions (AST → LINQ Expression Trees)
    ↓
ExpressionCompiler (Expression Trees → IL MethodBuilders)
    ↓
CLR execution with JSValue interop
```

### Project Structure

| Project | Target | Role |
|---------|--------|------|
| `YantraJS.Core` | net8.0 | Core engine: parser, compiler, runtime, built-in objects |
| `YantraJS.ExpressionCompiler` | net8.0 | LINQ expression → IL compilation |
| `YantraJS.JSClassGenerator` | net8.0 | Roslyn source generator for C#-to-JS bindings |
| `YantraJS.ModuleExtensions` | net8.0 | Module system extensions |
| `YantraJS.Network` | net8.0 | Network module support |
| `YantraJS.NodePollyfill` | net8.0 | Node.js compatibility polyfills |

### Key Subsystems in `YantraJS.Core`

| Subsystem | Location | Description |
|-----------|----------|-------------|
| **Parser** | `FastParser/` | JavaScript lexer (`FastScanner.cs`) and parser producing AST nodes |
| **AST** | `FastParser/Ast/` | Abstract syntax tree node types |
| **Compiler** | `FastParser/Compiler/`, `LinqExpressions/` | AST → LINQ expression tree compilation |
| **Runtime** | `Core/` (top-level) | `JSContext`, `JSValue`, `Arguments`, execution primitives |
| **Built-in objects** | `Core/{Array,String,Number,Object,Function,Date,Promise,...}/` | ECMAScript standard library |
| **CLR interop** | `Core/Clr/` | .NET type bridging (12 files) |
| **Module system** | `Core/Module/` | ES module loading, `import`/`export` |
| **Storage** | `Core/Storage/` | Internal memory/cache management (18 files) |
| **Debugger** | `Debugger/` | V8 inspector protocol (27 files) |
| **IL emission** | `Emit/` | Low-level IL helpers |
| **Code generation** | `CodeGen/`, `LambdaGen/` | Method generation utilities |

### Broiler Integration Points

- `src/Broiler.App/Rendering/ScriptEngine.cs` — main engine wrapper (`IScriptEngine`)
- `src/Broiler.App/Rendering/DomBridge.cs` — DOM shim attached to `JSContext`
- `src/Broiler.Cli/EngineTestService.cs` — smoke test (`1 + 2 == 3`)
- `src/Broiler.Cli/CaptureService.cs` — script execution for content capture

---

## Objectives

1. **Modularize** `YantraJS.Core` so subsystems have clear interfaces and can be maintained independently
2. **Create OS-independent tests** for each module to ensure robust cross-platform support
3. **Identify and implement structural optimizations** to improve code quality, performance, and scalability

---

## Action Items

### 1. Define Module Boundaries and Interfaces

- [ ] Map each subsystem (parser, compiler, runtime, built-ins, debugger, CLR interop, module system) to a clearly defined module with documented public API
- [ ] Identify coupling between subsystems (e.g., parser ↔ compiler, runtime ↔ built-ins)
- [ ] Define interfaces at module boundaries so implementations can be swapped or tested in isolation
- [ ] Document the compilation pipeline stages and the data contracts between them (source → AST → expressions → IL)

### 2. Plan Code Migration into Modules

- [ ] Split `YantraJS.Core` into **separate assemblies** per module (decided: assembly-level separation)
- [ ] Prioritize extraction order based on coupling analysis (least coupled first):
  1. **Parser** (`FastParser/`) — self-contained, produces AST
  2. **Expression compiler** (`LinqExpressions/`) — depends on AST, produces expressions
  3. **Built-in objects** (`Core/{Array,String,...}/`) — depend on runtime but not on each other
  4. **Debugger** (`Debugger/`) — largely independent; evaluate lighter alternatives to the V8 inspector protocol
  5. **CLR interop** (`Core/Clr/`) — depends on runtime, can be isolated
  6. **Module system** (`Core/Module/`) — depends on parser and runtime
- [ ] Create internal interfaces to decouple the monolithic `JSContext` from individual built-in object implementations

### 3. Design and Implement OS-Independent Test Cases

- [ ] Create a `YantraJS.Core.Tests` xunit project (net8.0) in the Broiler solution
- [ ] Test categories to cover:
  - **Parser tests**: Verify AST output for representative JS constructs (expressions, statements, functions, classes, modules)
  - **Compiler tests**: Verify expression tree generation from AST nodes
  - **Runtime tests**: `JSContext` lifecycle, `JSValue` type coercion, `Arguments` handling
  - **Built-in object tests**: Each built-in (Array, String, Number, Date, Promise, Map, Set, RegExp, JSON, etc.) with spec-conformance checks
  - **Module system tests**: `import`/`export` resolution, circular dependencies
  - **CLR interop tests**: .NET type bridging, method invocation, property access
  - **Integration tests**: End-to-end script execution matching expected output
- [ ] All tests must run on Linux, macOS, and Windows without OS-specific dependencies
- [ ] Use the existing `JIntPerfTests/Scripts/` benchmark scripts as a reference for integration coverage

### 4. Audit and Optimize

- [ ] Review items from `Improvements.md`:
  - Inverse switch optimization (convert switch argument based on case types)
  - Native type comparison (compare left/right arguments directly)
  - Startup time reduction (replace `LinkedStack` with ref struct-based type stack)
- [ ] Review known limitations from `UNSUPPORTED.md`:
  - Date Year 0 handling (CLR limitation)
  - Generator single-thread limitations
  - Number precision differences from V8/Firefox
  - Finally block return-control limitation
- [ ] Audit `Core/Storage/` (18 files) for memory management improvements
- [ ] Profile the compilation pipeline to identify bottlenecks
- [ ] Evaluate unsafe code usage (`AllowUnsafeBlocks`) for safety and necessity

### 5. Additional Improvements

- [ ] **Documentation**: Add XML doc comments to all public APIs in `YantraJS.Core`
- [ ] **Code style**: Establish and enforce consistent coding conventions across the codebase
- [ ] **Build system**: Add the existing test projects (`YantraTests`, `YantraJS.Core.Tests`, `YantraJS.ExpressionCompiler.Tests`) from `YantraJS.sln` to the main `Broiler.slnx` solution
- [ ] **CI integration**: Create GitHub Actions workflows to build and test on Linux, macOS, and Windows
- [ ] **Dependency updates**: Audit NuGet dependencies for updates and security patches (e.g., `System.Text.Json 10.0.3`, `ErrorProne.NET.Structs 0.3.0-beta.0`)
- [ ] **Performance benchmarks**: Integrate `JIntPerfTests` into CI with regression tracking
- [ ] **Source generator**: Review `YantraJS.JSClassGenerator` for correctness and add generator-specific tests

---

## Proposed Milestones

### Milestone 1 — Analysis and Test Foundation
- Complete subsystem mapping and coupling analysis
- Set up `YantraJS.Core.Tests` project with CI
- Write parser and runtime unit tests

### Milestone 2 — Module Extraction (Parser + Compiler)
- Extract parser with clean interface
- Extract expression compiler with clean interface
- Add compiler tests

### Milestone 3 — Built-in Object Isolation
- Define built-in object registration interface
- Add per-object test suites (Array, String, Number, Date, Promise, etc.)

### Milestone 4 — Debugger and CLR Interop
- [x] Evaluate lighter debugger protocol alternatives (e.g., DAP) and replace V8 inspector if a suitable option exists — see **DAP Evaluation** below
- [x] Isolate debugger behind interface (`IDebugger` in `Debugger/IDebugger.cs`)
- [x] Isolate CLR interop behind interface (`IClrInterop` in `Core/Clr/IClrInterop.cs`)
- [x] Add corresponding tests (`DebuggerTests.cs`, `ClrInteropTests.cs`)

### Milestone 5 — Optimization and Polish
- Implement improvements from `Improvements.md`
- Performance profiling and optimization
- Documentation and code style cleanup
- CI with cross-platform matrix

---

## Architectural Decisions

| Decision | Resolution |
|----------|------------|
| **Module separation strategy** | Separate assemblies (not namespace-only) |
| **ECMAScript conformance target** | Latest spec, adopted incrementally (step by step) |
| **Debugger protocol** | Retain V8 inspector protocol behind a new `IDebugger` interface — see **DAP Evaluation** below |
| **CLR interop** | Isolated behind `IClrInterop`; swappable via `JSContext.ClrInterop` static property |
| **Node.js polyfills (`modules/inbuilt/*.csx`)** | Consolidate into `YantraJS.NodePollyfill`; keep only actively used polyfills, remove dead `.csx` files, and convert remaining `.csx` scripts to compiled C# where feasible for better type safety and testability |

---

## DAP Evaluation

The Debug Adapter Protocol (DAP) was evaluated as a potential replacement for
the V8 inspector (Chrome DevTools Protocol) currently used by the debugger
subsystem.

### Finding: Retain V8 inspector protocol

| Criterion | V8 Inspector (current) | DAP |
|-----------|----------------------|-----|
| **Existing implementation** | 27 files, fully functional | Would require a complete rewrite |
| **Tooling ecosystem** | Chrome DevTools, VS Code via built-in CDP support | VS Code (native), other editors via adapters |
| **Protocol complexity** | Higher (full CDP surface) | Lower (focused on breakpoints, stepping, variables) |
| **Runtime integration** | Tight — `ScriptParsed`, `ConsoleApiCalled`, `GetProperties` already wired | Would need equivalent hooks |
| **Migration cost** | None | High — new server, new message types, new serialization |

**Decision:** The V8 inspector protocol is retained because:

1. **The new `IDebugger` interface already provides the decoupling benefit.**
   The runtime (`JSContext`) now depends only on `IDebugger`, not on
   `V8InspectorProtocol` or any concrete debugger.  A DAP implementation
   can be added later by implementing `IDebugger` without modifying the
   runtime.

2. **The existing V8 inspector implementation is complete and tested.**
   Replacing it would be a large effort with no immediate functional gain.

3. **DAP remains a viable future option.**  Because the debugger is now
   behind an interface, a DAP adapter can be introduced as a new
   `IDebugger` implementation without touching the existing V8 inspector
   code.

---

*Feedback and suggestions are welcome!*
