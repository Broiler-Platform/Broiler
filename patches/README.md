# Submodule patches awaiting upstream

Each file here is a `git format-patch` of a change that belongs in a submodule whose
remote is outside this session's GitHub scope, so the push returned 403 and the submodule
pointer was deliberately **not** bumped. Apply a patch inside its target submodule, push
there, then bump the gitlink in the parent — a pointer whose commit is not on the remote
would break CI, which clones submodules by pointer.

Delete a patch once its fix is upstream. This directory is a backlog, not an archive:
numbering restarts from `0001` against whatever is left, so the same number names
different changes at different times. Refer to a patch by its **commit subject**, never
by its number.

The two previous entries here — the Broiler.Regex Unicode/case-folding/iterative-matcher
work and the Broiler.JS Unicode-routing work — are gone because they are upstream: the
`Broiler.JS` and `Broiler.Regex` gitlinks now point at commits that contain them
(`Broiler.JS` at `d20e506`, `Broiler.Regex` at `4df3fb8`), so their patch files were
deleted and numbering restarted.

## Open patches

### `Enumerate symbol-keyed own properties in insertion order (§10.1.11.1)`

- **Target:** `Broiler.JS` (`Broiler-Platform/Broiler.JS`)
- **Apply onto:** `d20e506abbccad675fdb50b660e8417028f698b6`
- **File:** `0001-broiler-js-symbol-own-key-insertion-order.patch`

Track 1 of [the Broiler.JS gaps roadmap](../docs/broiler-js-gaps-roadmap.md#track-1--core-language-and-built-in-correctness).
`OrdinaryOwnPropertyKeys` (§10.1.11.1) lists symbol keys last, in the order the property was
added to the object. Integer and string keys got that order from the element array and the
`PropertySequence`, but symbol properties lived in a bare hash map keyed by the symbol's
creation id, which records no insertion order — so `getOwnPropertySymbols` sorted by creation
id, `Reflect.ownKeys` returned the map's raw hash order, and `Object.assign` copied in hash
order, all three disagreeing with each other and with the spec.

Each object now records its symbol keys' insertion order (mirroring how `PropertySequence`
orders string keys): a key is appended when first added, dropped on delete, and a
deleted-then-re-added key lands at the end while an in-place update keeps its position.
`JSObject.SymbolsInInsertionOrder` reads that record — reconciling any symbol a write path
missed so a key is never dropped — and every observable enumeration path
(`getOwnPropertySymbols`, `Reflect.ownKeys`, `Object.assign`/`CopyDataProperties`) goes
through it.

Verify with `dotnet test Broiler.JS/Broiler.JavaScript.Integration.Tests` (the new
`Track1LanguageTests` symbol-order cases; the one failure being the pre-existing unrelated
`M8ValidationTests.M8_DocumentationFiles_Exist`) and
`dotnet test Broiler.JS/Broiler.JavaScript.BuiltIns.Tests` (2214, unchanged).

### `Run async and generator bodies under the strict-mode runtime flag`

- **Target:** `Broiler.JS` (`Broiler-Platform/Broiler.JS`)
- **Apply onto:** `d20e506abbccad675fdb50b660e8417028f698b6`, **after** the symbol-order patch
  above (both add cases to `Track1LanguageTests.cs`, so applied in order they do not conflict)
- **File:** `0002-broiler-js-async-generator-strict-mode.patch`

Track 1 of [the Broiler.JS gaps roadmap](../docs/broiler-js-gaps-roadmap.md#track-1--core-language-and-built-in-correctness).
An async or generator body runs during its rewritten driver's steps, not during the
`JSFunction.InvokeFunction` call that created it, so it never inherited the `EnterStrictMode`
scope ordinary calls establish: a failing `[[Set]]` inside a `'use strict'` async/generator
body did not throw (it silently did nothing), even in the async function's synchronous prefix
or a generator before its first `yield`; and a strict async function's `this` was coerced to
the global object instead of left undefined.

`ClrGeneratorV2` now carries the function's strict flag and re-enters it around each body step
(`Next`), so the body observes strict `[[Set]]` semantics on every step and across
`yield`/`await`. The compiler sets `IsStrictMode` on the generator and on the async function's
inner generator, and passes `coerceThis: !isStrictFunction` to the async inner generator so a
strict async function's `this` is left undefined. The `StrictModeFlowTests` KnownGap test that
pinned the old no-throw behaviour is updated to assert `TypeError`.

Verify with `dotnet test Broiler.JS/Broiler.JavaScript.BuiltIns.Tests` (2214) and
`dotnet test Broiler.JS/Broiler.JavaScript.Integration.Tests` (the new `Track1LanguageTests`
async/generator cases; M8 the only, pre-existing, failure).

### `Raise the missing early SyntaxErrors for var/lexical conflicts, labelled-function loop bodies, and script exports`

- **Target:** `Broiler.JS` (`Broiler-Platform/Broiler.JS`)
- **Apply onto:** `d20e506abbccad675fdb50b660e8417028f698b6`, **after** the symbol-order and
  async/generator patches above (all three add cases to `Track1LanguageTests.cs`, so applied
  in that order — 0001, 0002, 0003 — they do not conflict)
- **File:** `0003-broiler-js-early-errors.patch`

Track 1 of [the Broiler.JS gaps roadmap](../docs/broiler-js-gaps-roadmap.md#track-1--core-language-and-built-in-correctness).
Three families of ECMAScript early SyntaxError were not raised, so the engine accepted — or,
in one case, crashed on — code the specification rejects at parse time:

- **VarDeclaredNames ∩ LexicallyDeclaredNames must be empty at every scope.** The parser only
  detected a `var`/lexical collision order-dependently and lost the information once a `var`
  hoisted out of the block it was written in, so `var x; let x;`, `let x; { var x; }`,
  `for (let x of []) { var x; }`, `try {} catch ([e]) { var e; }` and their siblings were
  accepted (the later declaration winning). Each `FastScopeItem` now records its own
  VarDeclaredNames as a `var` hoists through it, and rejects both a `var` hoisting into a
  lexical binding and a lexical declared where a `var` has hoisted through — at every scope
  level, in either order, and across a for-head, switch, or destructured catch parameter. A
  block-nested function declaration is lexical for this, so it conflicts with a same-named
  `var` while two block functions of one name still coexist in sloppy mode.
- **A labelled function declaration is never a legal loop body.** It was caught for a
  non-lexical head, but a `let`/`const` for-in/for-of/C-style head rewrites its body into a
  synthetic per-iteration block (`Desugar`), hiding the labelled statement from validation.
  The loop parser now rejects a labelled-function body on the statement as written, before
  that rewrite.
- **`export` in script code is an early error.** Its bindings target the host-injected module
  `exports` object, absent in a plain script; `export default <expr>` dereferenced the missing
  binding and surfaced a `NullReferenceException`. Every export form in a script now raises a
  clean `SyntaxError`.

Verify with `dotnet test Broiler.JS/Broiler.JavaScript.Integration.Tests` (the new
`Track1LanguageTests` early-error cases, with guards that valid var/lexical combinations in
different scopes, a `var` deduping against a parameter, a labelled function at statement
position, and duplicate sloppy block functions are all still accepted; M8 the only,
pre-existing, failure) and `dotnet test Broiler.JS/Broiler.JavaScript.BuiltIns.Tests` (2214,
unchanged).

## No main-repo fallback is needed

None of the three patches change behaviour the main repository depends on: without them the
pinned `Broiler.JS` submodule behaves exactly as it does today, so CI is unaffected until they
are applied. The parent build names none of the changed members, so leaving the submodule tree
at its pinned commit — as this branch does — leaves everything compiling.
