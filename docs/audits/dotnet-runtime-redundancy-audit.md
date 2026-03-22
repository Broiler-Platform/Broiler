# .NET Runtime Redundancy Audit

This document captures the results of auditing the codebase for custom
implementations that duplicate functionality already available in the .NET
runtime.

## Removed — Direct .NET Equivalents

### `ArraySpan<T>` (removed)

| Detail | Value |
|--------|-------|
| **File** | `Broiler.JavaScript.Ast/ArraySpan.cs` |
| **.NET equivalent** | `ArraySegment<T>`, `ReadOnlyMemory<T>`, `Span<T>` |
| **Status** | **Removed** — dead code |

`ArraySpan<T>` was a lightweight wrapper around `T[]` with a logical
`Length`.  The only producer (`FastList<T>.ToSpan()`) had **zero callers**,
and the only consumer (`VariableDeclarator.From(in ArraySpan<…>)`) was
never invoked.  All code paths were dead, so the type, its producer method,
and its consumer overload were deleted outright.

### `DelayTask` (removed)

| Detail | Value |
|--------|-------|
| **File** | `Broiler.JavaScript.Core/DelayTask.cs` |
| **.NET equivalent** | `Task.Delay(int, CancellationToken)` |
| **Status** | **Removed** — replaced with `Task.Delay` |

`DelayTask` created a `Task<bool>` that resolved to `true` on timeout and
`false` on cancellation.  Its sole caller (`AsyncQueue<T>.Process()`)
discarded the boolean result.  The call site was replaced with
`await Task.Delay(15000, c.Token)` plus a catch for
`OperationCanceledException`.

## Reviewed — Justified Custom Implementations

The following types overlap conceptually with .NET runtime facilities but
provide domain-specific behavior that prevents a direct swap.

### `StringSpan`

| Detail | Value |
|--------|-------|
| **File** | `Broiler.JavaScript.Ast/StringSpan.cs` |
| **Closest .NET type** | `ReadOnlySpan<char>` / `ReadOnlyMemory<char>` |
| **Why it is kept** | `ReadOnlySpan<char>` is a `ref struct` and cannot be stored in fields or used across `await` boundaries.  `StringSpan` is a regular struct that can be persisted in AST nodes, dictionaries (`StringMap<T>`), and other long-lived data structures.  It also includes domain-specific helpers (lazy substring materialization, custom hashing for `StringMap`, `ToCamelCase`, encoding) that have no built-in equivalent. |
| **Used in** | ~59 files across AST, Parser, Storage, Compiler, and Core assemblies |

### `ConcurrentStringMap<T>` / `ConcurrentUInt32Map<T>`

| Detail | Value |
|--------|-------|
| **Files** | `Broiler.JavaScript.Storage/ConcurrentStringMap\`1.cs` |
| **Closest .NET type** | `ConcurrentDictionary<TKey, TValue>` |
| **Why they are kept** | Keyed by `StringSpan` / `uint` with a custom trie-based `StringMap<T>` or `SAUint32Map<T>` underneath.  Replacing with `ConcurrentDictionary` would require first replacing `StringSpan` itself, and would lose the trie-based lookup performance characteristics that the JavaScript engine relies on. |

### `FastList<T>` / `FastStack<T>` / `FastPool`

| Detail | Value |
|--------|-------|
| **Files** | `Broiler.JavaScript.Parser/FastList.cs`, `FastStack.cs`, `FastPool.cs` |
| **Closest .NET types** | `List<T>`, `Stack<T>`, `ArrayPool<T>.Shared` |
| **Why they are kept** | The parser creates and discards hundreds of lists per parse.  `FastPool` provides type-specific array recycling (including `StringBuilder` pooling) in size-bucketed queues.  `FastList<T>` and `FastStack<T>` draw arrays from this pool to minimise GC pressure.  `ArrayPool<T>.Shared` covers part of this, but `FastPool` also pools `StringBuilder` instances and integrates disposal via `IDisposable` scoping.  A replacement would require profiling to ensure no performance regression. |

### `SparseList<T>`

| Detail | Value |
|--------|-------|
| **File** | `Broiler.JavaScript.Core/SparseList.cs` |
| **Closest .NET type** | `List<T>` |
| **Why it is kept** | Pages data in 8-element array segments rather than a single contiguous array.  This avoids full-copy resizing, which matters for the JavaScript object property storage where frequent growth is expected. |

### `StringMap<T>` / `SAUint32Map<T>`

| Detail | Value |
|--------|-------|
| **Files** | `Broiler.JavaScript.Storage/StringMap.cs`, `SAUint32Map.cs` |
| **Closest .NET types** | `Dictionary<string, T>`, `Dictionary<uint, T>` |
| **Why they are kept** | Trie-based structures optimised for `StringSpan`/`HashedString` keys and for the JavaScript engine's property-access patterns.  Replacing with `Dictionary` would require removing `StringSpan` first and may regress performance. |

### `AsyncPump`

| Detail | Value |
|--------|-------|
| **File** | `Broiler.JavaScript.Core/Core/Promise/AsyncPump.cs` |
| **Closest .NET type** | None (no built-in single-threaded synchronous pump) |
| **Why it is kept** | Implements a single-threaded `SynchronizationContext` to run async JavaScript promise chains synchronously on the calling thread.  There is no .NET runtime equivalent. |

### `JSJsonParser`

| Detail | Value |
|--------|-------|
| **File** | `Broiler.JavaScript.BuiltIns/Json/JSJsonParser.cs` |
| **Closest .NET type** | `System.Text.Json.JsonSerializer` |
| **Why it is kept** | Already built on top of `System.Text.Json` (`Utf8JsonReader`).  Adds ES2026 JSON reviver semantics with source-text tracking, which is not available in the standard library. |

### `MicroTaskQueue`

| Detail | Value |
|--------|-------|
| **File** | `src/Broiler.App/Rendering/MicroTaskQueue.cs` |
| **Closest .NET type** | None |
| **Why it is kept** | Models the HTML spec's microtask queue with drain semantics (tasks enqueued during drain run in the same cycle).  No .NET runtime equivalent. |

## Recommendations for Future Work

1. **Profile `FastPool` vs `ArrayPool<T>.Shared`** — if performance is
   comparable, `FastPool` could be simplified to delegate array allocation
   to `ArrayPool<T>.Shared`, reducing custom code.
2. **Evaluate `SparseList<T>`** — benchmark against `List<T>` for actual
   JavaScript property workloads to confirm the paging strategy is still
   beneficial on modern .NET.
3. **Consider `ReadOnlyMemory<char>` for new code** — while `StringSpan`
   cannot be removed from the parser without a large refactor, new features
   could prefer `ReadOnlyMemory<char>` where `ref struct` restrictions are
   not an issue.
