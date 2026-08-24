# Broiler.JS gaps — in progress

> Part of the [Broiler.JS gaps](broiler-js-gaps-roadmap.md) set:
> [closed](broiler-js-gaps-closed.md) · [open](broiler-js-gaps-open.md) · **in progress** · [won't fix](broiler-js-gaps-wont-fix.md).
> Statuses were last reconciled on **2026-08-24**. Every **fixed** entry names the pinned
> `Broiler.JS` commit that carries it and the regression that holds it.

Tracks that are part-landed: the mechanism is in and the evidence is real, but named steps remain
before the track's exit gate is met. What each still needs is listed as work, not as a gap.

## Track 0 — Restore trustworthy conformance evidence

**Status: the coverage gaps below are closed; the remaining work is a full pinned-corpus
run in CI and the product decisions for three `$262` hooks.**

What already landed is in [closed](broiler-js-gaps-closed.md#track-0--conformance-evidence).

### Remaining work

1. Run the pinned supported corpus in CI under the new protocol and modes. A local run of
   the 1494 files the modes unblocked (824 module, 30 raw, 640 `$262`; Debug host, so read
   it as shape rather than as a rate) executed all of them: raw 30/30 pass, `$262` 515 of
   640, module 332 of 824. The failures are engine work, mapped below — none of it is host
   coverage:
   - 141 module early errors that never fire (`dup-bound-names`, `await` as a module
     identifier, JSON module validation) → track 1;
   - 109 files whose specifier the parser rejects (`import defer`, import attributes) →
     track 1;
   - 52 that hang, nearly all dynamic `import()` of a module exporting a class or function
     → track 3;
   - 22 NullReferenceException crashes in module namespace and ambiguous-export paths →
     track 3. (A *distinct* module crash class — a read-only-write, not an NRE — was root-caused
     and fixed since: the module top-level lexical scope leak, see
     [track 3](#track-3--module-execution-semantics). Its effect on these 22 is unknown until the
     corpus is re-run on a pointer that carries the fix.);
   - 12 "invalid program" IL failures on top-level `for await` → track 1, **fixed** (see
     track 1's async/generator entry); re-measure this line on the next corpus run;
   - the `$262` remainder, mostly cross-realm identity and missing-throw cases → track 1.
2. Record product decisions for the three excluded `$262` hooks: `$262.agent` (112 files,
   multi-agent Atomics — owned by track 4), `$262.IsHTMLDDA` (42 files) and
   `$262.AbstractModuleSource` (8 files).
3. Enable `--include-negative` in release runs so negative-metadata totals are published.

**Exit gate:** deliberately failing and never-settling async fixtures fail deterministically;
every Test262 file is executed by an appropriate host mode or has a precise product-scope
exclusion; the dashboard records exact engine and suite revisions.

## Track 2 — Complete ECMAScript RegExp behavior

**Status: the Broiler.Regex engine gaps (actions 1–3, 5) are closed; the remaining work is
the one match-data abstraction that `Split` and `Replace` need (action 4), the pinned CI
run, and the translator retirement (action 6) that depends on both.**

What already landed is in [closed](broiler-js-gaps-closed.md#track-2--regexp).

### Remaining work

1. **Finish action 6 — matcher performance, then non-Unicode routing, then retire the
   translator.** All of Unicode mode routes to Broiler and quantifier repetition is iterative
   for *every* body shape, so no Unicode-mode pattern falls back on overflow — the matcher is
   the sole engine for Unicode mode. Non-Unicode patterns are still matched by .NET on purpose:
   a benchmark put the interpreter ~10× behind the compiled .NET engine, so routing the hot
   ASCII path now would regress it for no correctness gain (the non-Unicode *gap shapes* that
   .NET gets wrong already route). The first allocation pass has taken simple patterns to
   ~2–3× and the mean to ~6×; closing the rest is the compiled/bytecode path — defunctionalised
   continuations and an explicit backtrack stack so a capturing sequence stops allocating a
   closure chain per invocation and captures stop cloning the array. Once that lands and the
   hot path is competitive, routing non-Unicode — with captures, named groups, indices,
   `lastIndex`, species, replacement substitutions, and property order confirmed clean through
   the shared path — is what lets the source-to-source translator be removed.
2. **The pinned corpus in CI.** The focused test262 RegExp and UnicodeSets paths have not
   been run under the expanded routing; `scripts/compliance/test262-failures.txt` stays the
   path source and must not be reduced before CI confirms.
3. **Two deliberate divergences to settle,** both under `vi`, both pinned by a test in
   `UnicodeSetsTests`. For a lone *binary* property §22.2.2.9 folds the set (only a lone
   General_Category value is exempt), so `\p{ASCII}` holds `s` and matches `ſ`; V8 answers
   "no match" while agreeing on the equivalent literal range and on every other property.
   And a one-character `\q{…}` alternative folds like any other member, so `/[\q{A}]/vi`
   matches `A`; V8 folds the member but not the subject at that length — it matches `a`,
   not `A` — while canonicalizing both for a longer alternative. Broiler.Regex follows the
   spec in both; the product decision is whether to keep doing so.
4. **Human review.** `Broiler.Regex/HUMAN_REVIEW.md` is revision-scoped and its approval
   named a commit two changes ago; it needs re-running against the current revision.

See [the current limitations](../Broiler.JS/Broiler.Regex/Broiler.Regex/README.md#known-limitations-stubbed--todo),
[the RegExp roadmap](../Broiler.JS/Broiler.Regex/docs/roadmap.md), and
[the integration gate](../Broiler.JS/docs/roadmap/Component.md#3-finish-regexp-backend-adoption).

### Actions

1. ~~Connect Unicode property escapes to reviewed Broiler.Unicode property data.~~ Done.
2. ~~Implement and test UnicodeSets operands and string alternatives before routing `v`
   patterns.~~ Done.
3. ~~Implement complete mode-sensitive canonicalization and pin astral and multi-script
   cases.~~ Done.
4. ~~Move `Exec`, `Split`, and `Replace` to one match-data abstraction.~~ Done — all three
   read the routed engine through `EnumerateMatches`/`RunMatch`, `IJSRegExp.Value` is
   replaced by an engine-agnostic `IsMatch`, and a routed pattern no longer needs a .NET
   translation (`value` is null when the translator cannot represent it).
5. ~~Expand native routing only for syntax and semantics covered by focused and pinned
   corpus tests.~~ Done — every Unicode-mode (`u`/`v`) pattern Broiler can build now routes,
   validated by a JSRegExp-level differential against V8. Non-Unicode routing is deferred to
   the matcher-performance work.
6. Retire the translator only after captures, named groups, indices, `lastIndex`, species,
   replacement substitutions, and observable property order are clean — now gated solely on
   non-Unicode routing (remaining work item 1). Quantifier repetition is iterative for every
   body shape, so no Unicode-mode pattern falls back to .NET on overflow; the stack guard is
   a defensive backstop for pathological pattern nesting, unreachable by input size.

**Exit gate:** the pinned supported RegExp corpus is clean without sending unsupported syntax to
the native backend, and all public RegExp operations consume the same conforming match data.

## Track 3 — Module execution semantics

**Status: the module top-level lexical scope leak is root-caused and fixed, but the fix rides in a
`Broiler.JS` submodule patch that could not be pushed (the remote is outside this session's scope,
so the push returned 403 and the gitlink was not bumped). The remaining work is landing that patch
upstream and re-measuring the module corpus against it. The rest of track 3 — the host task model
and the JSON-module / `import.meta` / attribute-enforcement decisions — stays in
[open](broiler-js-gaps-open.md#track-3--scripts-tasks-and-modules).**

### Fixed in a patch awaiting upstream

- **A module's top-level `let`/`const`/`class` bindings shared one realm-wide slot per name.** They
  were published into the realm's global lexical environment exactly as a script's top-level
  lexicals are, so two modules declaring the same top-level name aliased one binding. A module that
  declared a top-level `const x` and, while its body was still running, triggered a transitive
  import of another module that also declared a top-level `const x` then wrote through the first
  module's read-only binding and threw "Cannot assign to read only variable". (Sibling imports at
  one level escaped only because each body had returned before the next ran, re-declaring the shared
  slot rather than double-occupying it.) **Fixed** by keeping a module's top-level lexicals local to
  its compiled body — the global-lexical publishing in `VisitProgram` is gated on the ES module
  goal, and the names an `export const`/`let`/`class` introduces are collected so an exported
  declaration follows the same module-local path as a bare one instead of falling through to a
  global-lexical slot. This is also the spec-correct scoping: an indirect eval in the global
  environment must not resolve a module's top-level bindings. The submodule commit is *Give a
  module's top-level lexicals their own environment*; regressions in `ModuleScopeIsolationTests`.
- **`export *` did not respect export precedence.** A star re-export republishes the source's names
  onto this module's `exports` at run time, and it overwrote a name the module already exported
  locally or via a named re-export — last-writer-wins, and a throw ("Cannot assign to read only
  variable") when the overwritten name was a `const` export and the star followed it in source
  order. ResolveExport (ES2024 16.2.1.5.3) consults a module's own local and indirect entries before
  its star entries, so an explicitly exported name is never taken from `export *`. **Fixed** in the
  same commit: the run-time copy skips a name the target already owns. (Two `export *` sources that
  both carry one name — a genuinely *ambiguous* star export, which should be excluded from the
  namespace and a SyntaxError to import by name — is a separate, unfixed case that the run-time-copy
  model cannot yet represent; it now resolves to the first star rather than the last, neither being
  conformant.)

### Remaining work

1. Land *Give a module's top-level lexicals their own environment* on `Broiler-Platform/Broiler.JS`
   and bump the submodule gitlink. Until then CI clones the pinned commit, which does not carry the
   fix, so the module scope-isolation cases stay red on CI and the patch under `patches/` is the only
   copy. There is no main-repo fallback and none is needed — the fix is internal to the `Broiler.JS`
   compiler and runtime and names no type the parent references.
2. Re-measure the module corpus against the landed fix. Track 0's run recorded "22
   NullReferenceException crashes in module namespace and ambiguous-export paths" and "52 that hang";
   this fix targets a distinct read-only-write crash class (not those NREs), so its effect on those
   totals is unknown until the corpus is re-run — do not assume it moves them.

**Exit gate:** a module's top-level bindings are isolated per module under transitive and cyclic
imports; `export *` obeys local/indirect/star precedence; the module corpus is re-run on a pointer
that carries the fix and its manifest reconciled.
