# ECMAScript Compliance Roadmap for YantraJS

This document is a comprehensive roadmap for updating the YantraJS JavaScript
engine (`yantra-1.2.295/`) to achieve full compliance with the latest ECMAScript
standard (ES2025, ratified June 2025) and to track forward-looking ES2026
proposals. It serves as a blueprint for contributors and a public reference for
project direction.

> **Scope:** This roadmap covers *language-level* compliance only — syntax,
> semantics, and built-in objects defined by ECMA-262. Host APIs (DOM, Node.js
> built-ins) are out of scope.

---

## Table of Contents

1. [Current Compliance Baseline](#1-current-compliance-baseline)
2. [ES2025 Feature Gap Analysis](#2-es2025-feature-gap-analysis)
3. [Existing Known Limitations](#3-existing-known-limitations)
4. [ES2026 Forward-Looking Features](#4-es2026-forward-looking-features)
5. [Implementation Milestones](#5-implementation-milestones)
6. [Conformance Testing Strategy](#6-conformance-testing-strategy)
7. [Progress Tracking](#7-progress-tracking)

---

## 1. Current Compliance Baseline

YantraJS currently provides broad coverage of ECMAScript features up to ES2024.
The table below summarizes the per-edition status.

| Edition | Year | Coverage | Notes |
|---------|------|----------|-------|
| ES5 | 2009 | ✅ Full | Strict mode only (sloppy mode intentionally unsupported) |
| ES2015 (ES6) | 2015 | ✅ Full | Arrow functions, classes, let/const, Map/Set, Promise, Symbols, modules, generators, destructuring, template literals, for-of, binary/octal literals |
| ES2016 | 2016 | ✅ Full | `Array.prototype.includes`, exponentiation `**` |
| ES2017 | 2017 | ✅ Full | async/await, `Object.entries`/`values`, string padding, trailing commas |
| ES2018 | 2018 | ✅ Full | Object rest/spread, for-await-of, async generators, `Promise.finally`, RegExp `s`/`u` flags |
| ES2019 | 2019 | ✅ Full | `Array.flat`/`flatMap`, `Object.fromEntries`, `String.trimStart`/`trimEnd`, optional catch binding, `Symbol.description` |
| ES2020 | 2020 | ✅ Full | Nullish coalescing `??`, optional chaining `?.`, BigInt, dynamic `import()`, `Promise.allSettled`, `globalThis`, logical assignment |
| ES2021 | 2021 | ⚠️ Mostly | `String.replaceAll`, `Promise.any`, numeric separators, `WeakRef`. **Gap:** `FinalizationRegistry` is partial |
| ES2022 | 2022 | ⚠️ Mostly | Private fields/methods, static blocks, `.at()`, `Error.cause`, `Object.hasOwn`. **Gap:** top-level await in non-module contexts |
| ES2023 | 2023 | ⚠️ Mostly | `using`/`await using`, `findLast`/`findLastIndex`, immutable array methods (`toReversed`, `toSorted`, `toSpliced`, `with`). **Gap:** Hashbang grammar `#!`, RegExp `v` flag partial |
| ES2024 | 2024 | ⚠️ Partial | `Promise.withResolvers`. **Gaps:** `ArrayBuffer.transfer`, `Atomics.waitAsync`, `Object.groupBy`/`Map.groupBy`, `String.isWellFormed`/`toWellFormed` full spec compliance |
| ES2025 | 2025 | ❌ Minimal | Most new features not yet implemented (see §2) |

### YantraJS Extensions (Non-Standard)

YantraJS includes several proprietary extensions:

| Extension | Description |
|-----------|-------------|
| Decimal type | `typeof 0.2m === 'decimal'`; decimal-aware `Math` operations |
| CLR interop | Seamless .NET ↔ JavaScript object marshalling |
| CSX modules | C# scripts (`.csx`) loadable as JS modules |
| Mixed module system | `require` and `import` coexist |

These extensions are orthogonal to ECMAScript compliance and are not tracked in
this roadmap.

---

## 2. ES2025 Feature Gap Analysis

ES2025 (16th Edition) was ratified in June 2025. The following features are new
in this edition and must be implemented for full compliance.

### 2.1 Iterator Helper Methods

**Status:** ❌ Not implemented
**Priority:** P0 — High
**Spec:** [tc39/proposal-iterator-helpers](https://github.com/tc39/proposal-iterator-helpers)

Adds functional-style helper methods directly to the `Iterator` prototype so
that any iterator (generators, `Map.entries()`, etc.) gains lazy transformation
methods.

| Sub-task | Description | Subsystem | Dependencies |
|----------|-------------|-----------|--------------|
| 2.1.1 | Create `Iterator` constructor and `Iterator.prototype` object | Runtime / Built-ins | None |
| 2.1.2 | Implement `Iterator.from(obj)` static method | Built-ins | 2.1.1 |
| 2.1.3 | Implement `Iterator.prototype.map(fn)` | Built-ins | 2.1.1 |
| 2.1.4 | Implement `Iterator.prototype.filter(fn)` | Built-ins | 2.1.1 |
| 2.1.5 | Implement `Iterator.prototype.take(n)` | Built-ins | 2.1.1 |
| 2.1.6 | Implement `Iterator.prototype.drop(n)` | Built-ins | 2.1.1 |
| 2.1.7 | Implement `Iterator.prototype.flatMap(fn)` | Built-ins | 2.1.1 |
| 2.1.8 | Implement `Iterator.prototype.reduce(fn, init)` | Built-ins | 2.1.1 |
| 2.1.9 | Implement `Iterator.prototype.toArray()` | Built-ins | 2.1.1 |
| 2.1.10 | Implement `Iterator.prototype.forEach(fn)` | Built-ins | 2.1.1 |
| 2.1.11 | Implement `Iterator.prototype.some(fn)` | Built-ins | 2.1.1 |
| 2.1.12 | Implement `Iterator.prototype.every(fn)` | Built-ins | 2.1.1 |
| 2.1.13 | Implement `Iterator.prototype.find(fn)` | Built-ins | 2.1.1 |
| 2.1.14 | Ensure existing generators inherit from `Iterator.prototype` | Runtime / Generators | 2.1.1 |
| 2.1.15 | Add unit tests for all iterator helper methods | Tests | 2.1.1–2.1.14 |

**Estimate:** 3–4 weeks

---

### 2.2 Set Methods

**Status:** ❌ Not implemented
**Priority:** P0 — High
**Spec:** [tc39/proposal-set-methods](https://github.com/tc39/proposal-set-methods)

Adds mathematical set operations to `Set.prototype`.

| Sub-task | Description | Subsystem | Dependencies |
|----------|-------------|-----------|--------------|
| 2.2.1 | Implement `Set.prototype.union(other)` | Built-ins (`Core/Set/`) | None |
| 2.2.2 | Implement `Set.prototype.intersection(other)` | Built-ins | None |
| 2.2.3 | Implement `Set.prototype.difference(other)` | Built-ins | None |
| 2.2.4 | Implement `Set.prototype.symmetricDifference(other)` | Built-ins | None |
| 2.2.5 | Implement `Set.prototype.isSubsetOf(other)` | Built-ins | None |
| 2.2.6 | Implement `Set.prototype.isSupersetOf(other)` | Built-ins | None |
| 2.2.7 | Implement `Set.prototype.isDisjointFrom(other)` | Built-ins | None |
| 2.2.8 | Add unit tests for all set methods | Tests | 2.2.1–2.2.7 |

**Estimate:** 1–2 weeks

---

### 2.3 Import Attributes & JSON Modules

**Status:** ✅ Implemented (parser + AST + compiler)
**Priority:** P1 — Medium
**Spec:** [tc39/proposal-import-attributes](https://github.com/tc39/proposal-import-attributes)

Allows import statements to carry attributes:
```js
import data from "./data.json" with { type: "json" };
```

| Sub-task | Description | Subsystem | Dependencies |
|----------|-------------|-----------|--------------|
| 2.3.1 | Add `with` clause parsing to `FastParser.VisitImport` | Parser | None |
| 2.3.2 | Add `ImportAttribute` AST node type | Parser / AST | 2.3.1 |
| 2.3.3 | Propagate attributes through compiler to module loader | Compiler | 2.3.2 |
| 2.3.4 | Implement JSON module loader (parse `.json` → default export) | Module system | 2.3.3 |
| 2.3.5 | Support `with` clause in dynamic `import()` | Parser / Compiler | 2.3.1, 2.3.3 |
| 2.3.6 | Reject unsupported attribute types at runtime | Runtime | 2.3.3 |
| 2.3.7 | Add parser and integration tests for import attributes | Tests | 2.3.1–2.3.6 |

**Estimate:** 2–3 weeks

---

### 2.4 Promise.try

**Status:** ❌ Not implemented
**Priority:** P1 — Medium
**Spec:** [tc39/proposal-promise-try](https://github.com/tc39/proposal-promise-try)

Wraps a synchronous-or-async function call into a promise:
```js
Promise.try(() => mayThrow())
```

| Sub-task | Description | Subsystem | Dependencies |
|----------|-------------|-----------|--------------|
| 2.4.1 | Add `Promise.try(fn, ...args)` static method | Built-ins (`Core/Promise/`) | None |
| 2.4.2 | Handle sync exceptions → rejected promise | Built-ins | 2.4.1 |
| 2.4.3 | Handle thenable return values | Built-ins | 2.4.1 |
| 2.4.4 | Add unit tests | Tests | 2.4.1–2.4.3 |

**Estimate:** 2–3 days

---

### 2.5 RegExp.escape

**Status:** ❌ Not implemented
**Priority:** P1 — Medium
**Spec:** [tc39/proposal-regex-escaping](https://github.com/tc39/proposal-regex-escaping)

Static method that escapes a string for safe embedding in a regular expression:
```js
RegExp.escape("hello.world") // "hello\\.world"
```

| Sub-task | Description | Subsystem | Dependencies |
|----------|-------------|-----------|--------------|
| 2.5.1 | Implement `RegExp.escape(str)` static method | Built-ins (`Core/RegExp/`) | None |
| 2.5.2 | Escape all syntax characters per spec (`.` `*` `+` `?` `^` `$` `{` `}` `(` `)` `|` `[` `]` `\` `/`) | Built-ins | 2.5.1 |
| 2.5.3 | Add unit tests | Tests | 2.5.1–2.5.2 |

**Estimate:** 1–2 days

---

### 2.6 RegExp Pattern Modifiers (Inline Flags)

**Status:** ✅ Implemented
**Priority:** P2 — Low
**Spec:** [tc39/proposal-regexp-modifiers](https://github.com/tc39/proposal-regexp-modifiers)

Allows changing flags within a regex subexpression:
```js
/(?i:abc)def/   // 'abc' is case-insensitive, 'def' is not
```

| Sub-task | Description | Subsystem | Dependencies |
|----------|-------------|-----------|--------------|
| 2.6.1 | Parse `(?flags:...)` modifier syntax in regex patterns | Parser / RegExp | None |
| 2.6.2 | Translate inline modifiers to .NET `RegexOptions` or pattern rewriting | Runtime / RegExp | 2.6.1 |
| 2.6.3 | Add unit tests | Tests | 2.6.1–2.6.2 |

**Note:** .NET `System.Text.RegularExpressions` supports inline modifiers
natively (`(?i:...)`) so this may map directly.

**Estimate:** 1 week

---

### 2.7 Duplicate Named Capturing Groups

**Status:** ✅ Implemented
**Priority:** P2 — Low
**Spec:** [tc39/proposal-duplicate-named-capturing-groups](https://github.com/tc39/proposal-duplicate-named-capturing-groups)

Allows the same named group in different alternatives:
```js
/(?<year>\d{4})-\d{2}|(?<year>\d{2})-\d{2}/
```

| Sub-task | Description | Subsystem | Dependencies |
|----------|-------------|-----------|--------------|
| 2.7.1 | Allow duplicate named groups in regex parsing (currently throws) | Parser / RegExp | None |
| 2.7.2 | Ensure `groups` object returns the matched alternative's value | Runtime / RegExp | 2.7.1 |
| 2.7.3 | Add unit tests | Tests | 2.7.1–2.7.2 |

**Note:** .NET regex supports duplicate named groups in newer versions, so this
may work with minimal changes.

**Estimate:** 3–5 days

---

### 2.8 Float16Array and Math.f16round

**Status:** ✅ Implemented
**Priority:** P2 — Low
**Spec:** [tc39/proposal-float16array](https://github.com/tc39/proposal-float16array)

Adds a 16-bit floating-point typed array and a rounding function.

| Sub-task | Description | Subsystem | Dependencies |
|----------|-------------|-----------|--------------|
| 2.8.1 | Implement `Float16Array` typed array class | Built-ins (`Core/Array/Typed/`) | None |
| 2.8.2 | Implement half-precision ↔ double conversion utilities | Runtime | 2.8.1 |
| 2.8.3 | Implement `Math.f16round(x)` | Built-ins (`Core/Objects/`) | 2.8.2 |
| 2.8.4 | Register `Float16Array` in `DataView` (`getFloat16`/`setFloat16`) | Built-ins | 2.8.1 |
| 2.8.5 | Add unit tests | Tests | 2.8.1–2.8.4 |

**Note:** .NET does not natively support `Half` in all contexts. A software
implementation for IEEE 754 half-precision may be needed, or use `System.Half`
(available since .NET 5).

**Estimate:** 1–2 weeks

---

### 2.9 ArrayBuffer.transfer and Related Methods

**Status:** ❌ Not implemented
**Priority:** P1 — Medium
**Spec:** [tc39/proposal-arraybuffer-transfer](https://github.com/tc39/proposal-arraybuffer-transfer)

Adds zero-copy buffer transfer and resize capabilities.

| Sub-task | Description | Subsystem | Dependencies |
|----------|-------------|-----------|--------------|
| 2.9.1 | Implement `ArrayBuffer.prototype.transfer(newLength?)` | Built-ins (`Core/Array/Typed/`) | None |
| 2.9.2 | Implement `ArrayBuffer.prototype.transferToFixedLength(newLength?)` | Built-ins | None |
| 2.9.3 | Implement `ArrayBuffer.prototype.detached` getter | Built-ins | None |
| 2.9.4 | Mark source buffer as detached after transfer | Runtime | 2.9.1 |
| 2.9.5 | Add unit tests | Tests | 2.9.1–2.9.4 |

**Estimate:** 1 week

---

### 2.10 Redeclarable Global eval-Declared vars

**Status:** ❓ Needs investigation
**Priority:** P2 — Low
**Spec:** [tc39/proposal-redeclarable-global-eval-vars](https://github.com/nicolo-ribaudo/proposal-redeclarable-global-eval-vars)

Allows `eval`-introduced `var` declarations to coexist with `let`/`const`
globals in scripts.

| Sub-task | Description | Subsystem | Dependencies |
|----------|-------------|-----------|--------------|
| 2.10.1 | Audit current `eval` scoping behavior | Runtime / Compiler | None |
| 2.10.2 | Update variable binding logic if needed | Runtime | 2.10.1 |
| 2.10.3 | Add unit tests | Tests | 2.10.2 |

**Estimate:** 2–3 days

---

## 3. Existing Known Limitations

These pre-existing issues (documented in `UNSUPPORTED.md`) affect spec
compliance and should be addressed alongside the ES2025 work.

| ID | Limitation | Impact | Root Cause | Suggested Fix |
|----|-----------|--------|------------|---------------|
| L1 | `Date.prototype.setFullYear(0)` returns Invalid Date | Minor | .NET `DateTime` does not support year 0 | Map year 0 to proleptic calendar or use `DateTimeOffset` |
| L2 | `Intl.DateTimeFormat` not fully supported | Minor | Relies on .NET string formatting | Implement spec-conformant `Intl.DateTimeFormat` using ICU4N or .NET globalization APIs |
| L3 | Generator single-thread resumption | Medium | CLR lacks resumable functions | Continue GeneratorsV2 expression-tree rewriting approach |
| L4 | Number precision (15 vs 17 digits) | Minor | .NET `double.ToString` differences | Use `"R"` or `"G17"` format specifier for round-trip fidelity |
| L5 | Return from `finally` block not supported | Minor | LINQ lambda compiler limitation | Rewrite `finally` to avoid lambda return restriction |
| L6 | Array destructuring elision (`var [a,,c]`) | Medium | Parser / compiler gap | Add elision support to destructuring visitor |
| L7 | `Date` constructor defaults day to 0 (should be 1) | Minor | `Arguments.Get7Int` uses 0 default | Change default parameter from 0 to 1 |
| L8 | Promise requires `SynchronizationContext` | Medium | `AsyncPump` dependency | Allow promise resolution without sync context, or provide built-in context |

---

## 4. ES2026 Forward-Looking Features

These proposals have reached TC39 Stage 4 or are expected for ES2026 (17th
Edition). They are tracked here for planning purposes but are lower priority
than ES2025 compliance.

### 4.1 Temporal API

**Status:** ❌ Not implemented
**Priority:** P1 — Medium (large scope)
**Spec:** [tc39/proposal-temporal](https://github.com/tc39/proposal-temporal)

A comprehensive date/time API replacing `Date`. This is the largest single
feature expected in ES2026.

| Sub-task | Description | Estimate |
|----------|-------------|----------|
| 4.1.1 | Implement `Temporal.PlainDate` | 1 week |
| 4.1.2 | Implement `Temporal.PlainTime` | 1 week |
| 4.1.3 | Implement `Temporal.PlainDateTime` | 1 week |
| 4.1.4 | Implement `Temporal.ZonedDateTime` | 2 weeks |
| 4.1.5 | Implement `Temporal.Instant` | 3 days |
| 4.1.6 | Implement `Temporal.Duration` | 1 week |
| 4.1.7 | Implement `Temporal.PlainYearMonth` / `PlainMonthDay` | 3 days |
| 4.1.8 | Implement `Temporal.Calendar` and `Temporal.TimeZone` | 2 weeks |
| 4.1.9 | Implement `Temporal.Now` | 2 days |
| 4.1.10 | Add comprehensive test suite | 2 weeks |

**Total estimate:** 8–12 weeks

---

### 4.2 Math.sumPrecise

**Status:** ✅ Implemented
**Priority:** P2 — Low
**Spec:** [tc39/proposal-math-sum](https://github.com/tc39/proposal-math-sum)

| Sub-task | Description | Estimate |
|----------|-------------|----------|
| 4.2.1 | Implement `Math.sumPrecise(iterable)` using compensated summation (Kahan/Neumaier) | 2–3 days |
| 4.2.2 | Add unit tests with precision edge cases | 1 day |

---

### 4.3 Uint8Array Base64 / Hex Methods

**Status:** ✅ Implemented
**Priority:** P2 — Low
**Spec:** [tc39/proposal-arraybuffer-base64](https://github.com/tc39/proposal-arraybuffer-base64)

| Sub-task | Description | Estimate |
|----------|-------------|----------|
| 4.3.1 | Implement `Uint8Array.fromBase64(str)` | 1–2 days |
| 4.3.2 | Implement `Uint8Array.prototype.toBase64()` | 1 day |
| 4.3.3 | Implement `Uint8Array.fromHex(str)` | 1 day |
| 4.3.4 | Implement `Uint8Array.prototype.toHex()` | 1 day |
| 4.3.5 | Implement `Uint8Array.prototype.setFromBase64()` / `setFromHex()` | 1 day |
| 4.3.6 | Add unit tests | 1 day |

---

### 4.4 Error.isError

**Status:** ❌ Not implemented
**Priority:** P2 — Low
**Spec:** [tc39/proposal-is-error](https://github.com/tc39/proposal-is-error)

| Sub-task | Description | Estimate |
|----------|-------------|----------|
| 4.4.1 | Implement `Error.isError(value)` static method | 1 day |
| 4.4.2 | Add unit tests | 1 day |

---

### 4.5 Array.fromAsync

**Status:** ❌ Not implemented
**Priority:** P2 — Low
**Spec:** [tc39/proposal-array-from-async](https://github.com/tc39/proposal-array-from-async)

| Sub-task | Description | Estimate |
|----------|-------------|----------|
| 4.5.1 | Implement `Array.fromAsync(asyncIterable, mapFn?, thisArg?)` | 3–5 days |
| 4.5.2 | Add unit tests | 1 day |

---

### 4.6 Object.groupBy / Map.groupBy

**Status:** ❌ Not implemented
**Priority:** P1 — Medium
**Spec:** [tc39/proposal-array-grouping](https://github.com/tc39/proposal-array-grouping)

| Sub-task | Description | Estimate |
|----------|-------------|----------|
| 4.6.1 | Implement `Object.groupBy(iterable, callback)` | 2–3 days |
| 4.6.2 | Implement `Map.groupBy(iterable, callback)` | 1–2 days |
| 4.6.3 | Add unit tests | 1 day |

---

### 4.7 JSON.parse Source Text Access

**Status:** ✅ Implemented
**Priority:** P3 — Low
**Spec:** [tc39/proposal-json-parse-with-source](https://github.com/tc39/proposal-json-parse-with-source)

| Sub-task | Description | Estimate |
|----------|-------------|----------|
| 4.7.1 | Add `context` parameter to `JSON.parse` reviver | 2–3 days |
| 4.7.2 | Provide `source` property in reviver context | 2 days |
| 4.7.3 | Add unit tests | 1 day |

---

### 4.8 Iterator.concat

**Status:** ❌ Not implemented
**Priority:** P2 — Low
**Spec:** [tc39/proposal-iterator-sequencing](https://github.com/tc39/proposal-iterator-sequencing)

| Sub-task | Description | Estimate |
|----------|-------------|----------|
| 4.8.1 | Implement `Iterator.concat(...iterables)` | 2–3 days |
| 4.8.2 | Add unit tests | 1 day |

**Dependency:** §2.1 (Iterator helpers) should be implemented first.

---

### 4.9 Map.getOrInsert (Upsert)

**Status:** ✅ Implemented
**Priority:** P2 — Low
**Spec:** [tc39/proposal-upsert](https://github.com/tc39/proposal-upsert)

| Sub-task | Description | Estimate |
|----------|-------------|----------|
| 4.9.1 | Implement `Map.prototype.getOrInsert(key, defaultValue)` | 1–2 days |
| 4.9.2 | Implement `Map.prototype.getOrInsertComputed(key, callback)` | 1–2 days |
| 4.9.3 | Implement `WeakMap.prototype.getOrInsert` / `getOrInsertComputed` | 1 day |
| 4.9.4 | Add unit tests | 1 day |

---

### 4.10 Atomics and SharedArrayBuffer

**Status:** ❌ Not implemented
**Priority:** P2 — Low (primarily relevant for multi-threaded environments)

| Sub-task | Description | Estimate |
|----------|-------------|----------|
| 4.10.1 | Implement `SharedArrayBuffer` | 1–2 weeks |
| 4.10.2 | Implement `Atomics` static methods (`add`, `sub`, `and`, `or`, `xor`, `load`, `store`, `exchange`, `compareExchange`, `wait`, `notify`, `isLockFree`) | 2–3 weeks |
| 4.10.3 | Add unit tests | 1 week |

**Note:** SharedArrayBuffer semantics require careful consideration of CLR
threading model. This is architecturally complex and may need design review.

---

### 4.11 structuredClone

**Status:** ✅ Implemented
**Priority:** P1 — Medium (widely used in practice)

| Sub-task | Description | Estimate |
|----------|-------------|----------|
| 4.11.1 | Implement `structuredClone(value, options?)` global function | 1 week |
| 4.11.2 | Support structured clone algorithm for all built-in types (Date, RegExp, Map, Set, ArrayBuffer, TypedArray, Error, etc.) | 1–2 weeks |
| 4.11.3 | Handle circular references and transfer lists | 3–5 days |
| 4.11.4 | Add unit tests | 3 days |

**Note:** `structuredClone` is defined by the HTML spec (not ECMA-262) but is
universally expected in JavaScript engines.

---

## 5. Implementation Milestones

### Phase 1 — Quick Wins and Foundation (Weeks 1–4)

High-impact, low-effort features that immediately improve compliance.

| Task | Feature | Priority | Estimate | Dependencies |
|------|---------|----------|----------|--------------|
| §2.5 | `RegExp.escape` | P1 | 1–2 days | None |
| §2.4 | `Promise.try` | P1 | 2–3 days | None |
| §4.4 | `Error.isError` | P2 | 1 day | None |
| §4.6 | `Object.groupBy` / `Map.groupBy` | P1 | 3–5 days | None |
| §L7 | Fix `Date` constructor day default (0 → 1) | — | 1 day | None |
| §L6 | Fix array destructuring elision | — | 2–3 days | None |
| §2.2 | Set methods (union, intersection, etc.) | P0 | 1–2 weeks | None |
| §2.10 | Redeclarable global eval vars | P2 | 2–3 days | None |

### Phase 2 — Iterator Helpers and Core ES2025 (Weeks 5–10)

The largest block of ES2025 work.

| Task | Feature | Priority | Estimate | Dependencies |
|------|---------|----------|----------|--------------|
| §2.1 | Iterator helper methods | P0 | 3–4 weeks | None |
| §4.8 | `Iterator.concat` | P2 | 2–3 days | §2.1 |
| §2.9 | `ArrayBuffer.transfer` | P1 | 1 week | None |
| §4.5 | `Array.fromAsync` | P2 | 3–5 days | None |

### Phase 3 — Module System and RegExp (Weeks 11–14)

Features that require parser and compiler changes.

| Task | Feature | Priority | Estimate | Dependencies |
|------|---------|----------|----------|--------------|
| §2.3 | Import attributes & JSON modules | P1 | 2–3 weeks | None |
| §2.6 | RegExp pattern modifiers | P2 | 1 week | None |
| §2.7 | Duplicate named capture groups | P2 | 3–5 days | None |
| §2.8 | `Float16Array` / `Math.f16round` | P2 | 1–2 weeks | None |

### Phase 4 — ES2026 and Advanced Features (Weeks 15–24)

Forward-looking features for the next edition.

| Task | Feature | Priority | Estimate | Dependencies |
|------|---------|----------|----------|--------------|
| §4.2 | `Math.sumPrecise` | P2 | 2–3 days | None |
| §4.3 | Uint8Array Base64/Hex | P2 | 3–5 days | None |
| §4.7 | JSON.parse source text access | P3 | 3–5 days | None |
| §4.9 | `Map.getOrInsert` (upsert) | P2 | 2–3 days | None |
| §4.11 | `structuredClone` | P1 | 2–3 weeks | None |
| §4.1 | Temporal API | P1 | 8–12 weeks | None |

### Phase 5 — Hardening and Known Limitations (Ongoing)

Address pre-existing spec deviations.

| Task | Feature | Priority | Estimate | Dependencies |
|------|---------|----------|----------|--------------|
| §L1 | Date year 0 handling | — | 2–3 days | None |
| §L2 | `Intl.DateTimeFormat` compliance | — | 2–4 weeks | None |
| §L3 | Generator single-thread resumption | — | 4–6 weeks | GeneratorsV2 |
| §L4 | Number precision (G17 format) | — | 1–2 days | None |
| §L5 | Return from `finally` block | — | 1 week | None |
| §L8 | Promise without SynchronizationContext | — | 1–2 weeks | None |

---

## 6. Conformance Testing Strategy

### 6.1 Test262 Integration

[Test262](https://github.com/tc39/test262) is the official ECMAScript
conformance test suite maintained by TC39. Integrating it is critical for
validating compliance.

| Step | Description | Estimate |
|------|-------------|----------|
| 6.1.1 | Add Test262 as a Git submodule under `yantra-1.2.295/test262/` | 1 day |
| 6.1.2 | Build a Test262 harness in C# that loads and executes test files via `JSContext` | 1–2 weeks |
| 6.1.3 | Implement Test262 harness helpers (`$262`, `assert`, `assert.throws`, `assert.sameValue`, etc.) | 1 week |
| 6.1.4 | Run initial baseline — measure pass/fail counts across all test categories | 3–5 days |
| 6.1.5 | Establish a "known-failure" list to track regressions vs. known gaps | 2–3 days |
| 6.1.6 | Add Test262 to CI — run on each PR, fail on regressions | 2–3 days |
| 6.1.7 | Generate per-feature compliance reports from Test262 results | 1 week |

### 6.2 Unit Tests (Existing Framework)

The existing `YantraJS.Core.Tests` xunit project should continue to be the
primary vehicle for feature-level tests.

| Practice | Description |
|----------|-------------|
| **Per-feature test class** | Each new feature (e.g., `IteratorHelperTests.cs`, `SetMethodTests.cs`) gets its own test class |
| **Eval-based tests** | Tests should use `JSContext.Eval(script)` to execute JavaScript and assert on the result |
| **Negative tests** | Each feature must include tests for expected errors (`TypeError`, `SyntaxError`, etc.) |
| **Edge cases** | Empty iterators, `null`/`undefined` arguments, prototype chain interactions |
| **Cross-platform** | All tests must pass on Linux, macOS, and Windows (CI matrix already configured) |

### 6.3 Regression Testing

| Practice | Description |
|----------|-------------|
| **No test removal** | Existing tests must not be removed or weakened |
| **Differential testing** | Compare YantraJS output against V8/Node.js for complex scripts |
| **Known-failure tracking** | Maintain a list of known Test262 failures with issue references |

---

## 7. Progress Tracking

### Dashboard

Use this table to track implementation status at a glance. Update the status
column as work progresses.

| § | Feature | Status | Assignee | PR |
|---|---------|--------|----------|----|
| 2.1 | Iterator helpers | ✅ Done | @copilot | Phase 2 PR |
| 2.2 | Set methods | ✅ Done | @copilot | Phase 1 PR |
| 2.3 | Import attributes | ✅ Done | @copilot | Phase 3 PR |
| 2.4 | Promise.try | ✅ Done | @copilot | Phase 1 PR |
| 2.5 | RegExp.escape | ✅ Done | @copilot | Phase 1 PR |
| 2.6 | RegExp modifiers | ✅ Done | @copilot | Phase 3 PR |
| 2.7 | Duplicate named groups | ✅ Done | @copilot | Phase 3 PR |
| 2.8 | Float16Array | ✅ Done | @copilot | Phase 3 PR |
| 2.9 | ArrayBuffer.transfer | ✅ Done | @copilot | Phase 2 PR |
| 2.10 | Redeclarable eval vars | ⏳ Audited — eval scope update needs compiler work | — | — |
| 4.1 | Temporal API | ❌ Not started | — | — |
| 4.2 | Math.sumPrecise | ✅ Done | @copilot | Phase 4 PR |
| 4.3 | Uint8Array Base64/Hex | ✅ Done | @copilot | Phase 4 PR |
| 4.4 | Error.isError | ✅ Done | @copilot | Phase 1 PR |
| 4.5 | Array.fromAsync | ✅ Done | @copilot | Phase 2 PR |
| 4.6 | Object.groupBy / Map.groupBy | ✅ Done | @copilot | Phase 1 PR |
| 4.7 | JSON.parse source text | ✅ Done | @copilot | Phase 4 PR |
| 4.8 | Iterator.concat | ✅ Done | @copilot | Phase 2 PR |
| 4.9 | Map.getOrInsert | ✅ Done | @copilot | Phase 4 PR |
| 4.10 | Atomics / SharedArrayBuffer | ❌ Not started | — | — |
| 4.11 | structuredClone | ✅ Done | @copilot | Phase 4 PR |
| L1 | Date year 0 | ❌ Not started | — | — |
| L2 | Intl.DateTimeFormat | ❌ Not started | — | — |
| L3 | Generator resumption | ⏳ In progress | — | — |
| L4 | Number precision | ❌ Not started | — | — |
| L5 | Finally return | ❌ Not started | — | — |
| L6 | Destructuring elision | ✅ Done | @copilot | Phase 1 PR |
| L7 | Date day default | ✅ Done | @copilot | Phase 1 PR |
| L8 | Promise SyncContext | ❌ Not started | — | — |

### Status Legend

| Icon | Meaning |
|------|---------|
| ❌ | Not started |
| ❓ | Needs investigation |
| ⏳ | In progress |
| 🔍 | In review |
| ✅ | Complete |

### How to Contribute

1. Pick a task from the [Dashboard](#dashboard) that shows ❌ Not started
2. Comment on the tracking issue to claim the task
3. Create a feature branch: `feature/es2025-{feature-name}`
4. Implement the feature with unit tests in `YantraJS.Core.Tests`
5. Run existing tests to verify no regressions: `dotnet test yantra-1.2.295/YantraJS.Core.Tests`
6. Submit a PR referencing the task number (e.g., "Implement §2.2 Set methods")
7. Update the Dashboard status once merged

---

## References

- [ECMA-262, 16th Edition (ES2025)](https://ecma-international.org/publications-and-standards/standards/ecma-262/)
- [TC39 Finished Proposals](https://github.com/tc39/proposals/blob/main/finished-proposals.md)
- [TC39 Active Proposals](https://github.com/tc39/proposals)
- [Test262 Conformance Suite](https://github.com/tc39/test262)
- [YantraJS Refactoring Roadmap](REFACTORING_ROADMAP.md)
- [YantraJS Known Limitations](UNSUPPORTED.md)
- [YantraJS Subsystem Map](SUBSYSTEM_MAP.md)

---

*Last updated: 2026-03-09*
