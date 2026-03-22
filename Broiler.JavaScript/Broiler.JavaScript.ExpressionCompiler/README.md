# Broiler.JavaScript.ExpressionCompiler — Internal Architecture

## Overview

This assembly provides the custom expression-tree-to-IL compilation pipeline. It replaces `System.Linq.Expressions.Expression` with a lighter-weight `YExpression` tree that supports additional operations (yield, jump-switch, address-of) and emits optimized IL via `System.Reflection.Emit`.

**150 files, ~8,861 LOC** — the largest assembly by file count, but with a cohesive, single-purpose design.

## Directory Structure

```
ExpressionCompiler/
├── Expressions/          (54 files) — Y-prefixed expression tree nodes
│   ├── YExpression.cs              — Abstract base class for all nodes
│   ├── YExpressionType.cs          — Node type enumeration
│   ├── YExpressionVisitor.cs       — Abstract visitor base class
│   ├── YBinaryExpression.cs        — Binary operations (+, -, *, /, etc.)
│   ├── YUnaryExpression.cs         — Unary operations (!, ~, -, typeof)
│   ├── YConstantExpression.cs      — Literal constants
│   ├── YBlockExpression.cs         — Statement blocks with variables
│   ├── YLambdaExpression.cs        — Lambda/function definitions
│   ├── YCallExpression.cs          — Method calls (static + instance)
│   ├── YConditionalExpression.cs   — Ternary/if expressions
│   ├── YTryCatchFinallyExpression  — Exception handling
│   ├── YYieldExpression.cs         — Generator yield points
│   ├── YJumpSwitchExpression.cs    — Optimized switch dispatch
│   └── ... (40+ more node types)
│
├── Generator/            (45 files) — IL code generation (visitor pattern)
│   ├── ILCodeGenerator.cs          — Main class (partial), visitor dispatch
│   ├── ILCodeGenerator.Visit.cs    — General visit dispatch
│   ├── ILCodeGenerator.VisitBinary.cs   — IL emission for binary ops
│   ├── ILCodeGenerator.VisitCall.cs     — IL emission for method calls
│   ├── ILCodeGenerator.VisitLambda.cs   — IL emission for lambdas
│   ├── Variable.cs                 — Local variable tracking
│   ├── LabelInfo.cs                — Label/branch target tracking
│   └── ... (38+ more visitor partials)
│
├── Converters/           (9 files)  — System.Linq.Expressions → YExpression
│   ├── LinqConverter.cs            — Main converter class
│   ├── LinqConverter.Visit*.cs     — Partial visitor methods
│   ├── LinqConverters.cs           — Converter factory/registry
│   ├── LinqMap.cs                  — Type mapping infrastructure
│   └── LabelMap.cs                 — Label mapping infrastructure
│
├── Core/                 (15 files) — Shared infrastructure
│   ├── ILWriter.cs                 — IL instruction emission wrapper
│   ├── Sequence.cs                 — Fast enumerable collection
│   ├── IFastEnumerable.cs          — High-perf enumeration interface
│   ├── LinkedStack.cs              — Stack for scope tracking
│   └── ... (11 more utility types)
│
├── Runtime/              (3 files)  — Runtime method management
│   ├── RuntimeAssembly.cs          — Dynamic assembly management
│   ├── RuntimeMethodBuilder.cs     — Dynamic method creation
│   └── MethodRepository.cs         — Compiled method cache
│
├── ClosureSeparator/     (1 file)   — Closure variable boxing
│   └── Box.cs                      — Closure variable container
│
├── SL/                   (1 file)   — Lambda conversion
│   └── LambdaConverter.cs          — Lambda expression adapter
│
├── Closures/             (1 file)   — Closure infrastructure
│   └── Closures.cs                 — Closure resolution
│
└── Root files            (17 files) — Top-level utilities
    ├── ExpressionCompiler.cs       — Main entry point, IMethodRepository
    ├── IMethodBuilder.cs           — Method builder interface
    ├── LambdaMethodBuilder.cs      — Lambda compilation pipeline
    ├── TypeExtensions.cs           — Reflection helpers
    ├── LinqExtensions.cs           — Expression tree extensions
    ├── StackGuard.cs               — Stack overflow protection
    └── ... (11 more utility files)
```

## Compilation Pipeline

```
JavaScript Source
      │
      ▼
  FastParser (Parser assembly)
      │
      ▼
  AST Nodes (Ast assembly)
      │
      ▼
  FastCompiler (Compiler assembly)
      │  Uses LinqExpressions builders (Core assembly)
      │  to construct YExpression trees
      ▼
  YExpression Tree (this assembly)
      │
      ▼
  ILCodeGenerator (this assembly)
      │  Visits each Y-node and emits IL instructions
      │  via ILWriter → System.Reflection.Emit
      ▼
  Dynamic Method (CLR runtime)
      │
      ▼
  Execution
```

## Key Design Decisions

### Why Y-expressions instead of System.Linq.Expressions?

`System.Linq.Expressions.Expression` is designed for LINQ query trees and has limitations:
- No `yield` support (required for JS generators)
- No optimized switch dispatch (required for generator state machines)
- No address-of expressions (required for ref-parameter marshalling)
- Heavy allocation overhead for simple constant expressions

The Y-expression tree is a purpose-built, lightweight alternative that supports all JavaScript language features while minimizing allocation overhead.

### Why not split this assembly?

Evaluated in M13 (Phase 2). Findings:

1. **Tight coupling by design**: ILCodeGenerator has 45 partial files with 1:1 correspondence to Y-expression types. Each visitor method directly takes a specific Y-type parameter (`VisitBinary(YBinaryExpression)`). This is intentional for type-safe IL emission.

2. **All consumers need all groups**: Any assembly that compiles JavaScript needs both the expression types (to build trees) AND the IL generator (to execute them). Splitting would force every consumer to reference both sub-assemblies.

3. **Performance-critical code**: The visitor pattern with compile-time type dispatch avoids virtual calls in the inner compilation loop. Adding an abstraction layer between Y-types and the generator would degrade compilation throughput.

4. **Cohesive purpose**: The assembly has a single responsibility — "convert expression trees to IL" — that naturally encompasses both the tree structure and the code generator.

**Decision: No-go on decomposition. The monolithic structure is architecturally correct.**

## Extending

### Adding a new expression type

1. Create `Expressions/YMyNewExpression.cs` inheriting `YExpression`
2. Add a new entry to `YExpressionType` enum
3. Add `VisitMyNew(YMyNewExpression)` abstract method to `YExpressionVisitor`
4. Create `Generator/ILCodeGenerator.VisitMyNew.cs` with the IL emission logic
5. (Optional) Add a `LinqConverter` partial if the expression maps from `System.Linq.Expressions`

### Adding static factory methods

All expression construction goes through static factory methods on `YExpression`:
```csharp
YExpression.Binary(left, YOperator.Add, right)
YExpression.Constant(42)
YExpression.Block(variables, statements)
```
