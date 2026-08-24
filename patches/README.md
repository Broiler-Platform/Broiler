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
`Track1LanguageTests` symbol-order cases; 5030 of 5031, the one failure being the pre-existing
unrelated `M8ValidationTests.M8_DocumentationFiles_Exist`) and
`dotnet test Broiler.JS/Broiler.JavaScript.BuiltIns.Tests` (2214, unchanged).

## No main-repo fallback is needed

The patch changes no behaviour the main repository depends on: without it the pinned
`Broiler.JS` submodule behaves exactly as it does today, so CI is unaffected until it is
applied. The parent build names none of the changed members, so leaving the submodule tree
at its pinned commit — as this branch does — leaves everything compiling.
