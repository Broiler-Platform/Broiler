# Broiler.JS gaps — in progress

> Part of the [Broiler.JS gaps](broiler-js-gaps-roadmap.md) set:
> [closed](broiler-js-gaps-closed.md) · [open](broiler-js-gaps-open.md) · **in progress** · [won't fix](broiler-js-gaps-wont-fix.md).
> Statuses were last reconciled on **2026-08-26**.

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
     [closed](broiler-js-gaps-closed.md#track-1--direct-eval-parser-scope-and-control-flow), where
     both the top-level-`for await` flag and the unsettled-step-result deadlock are recorded);
     re-measure this line on the next corpus run;
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

Actions 1-5 of the original list are complete — property escapes, UnicodeSets operands and string
alternatives, mode-sensitive canonicalization, the one match-data abstraction behind
`Exec`/`Split`/`Replace`, and native routing for every Unicode-mode pattern Broiler can build. What
they landed is recorded in [closed](broiler-js-gaps-closed.md#track-2--regexp). One action is left:

1. Retire the translator only after captures, named groups, indices, `lastIndex`, species,
   replacement substitutions, and observable property order are clean — now gated solely on
   non-Unicode routing (remaining work item 1). Quantifier repetition is iterative for every
   body shape, so no Unicode-mode pattern falls back to .NET on overflow; the stack guard is
   a defensive backstop for pathological pattern nesting, unreachable by input size.

**Exit gate:** the pinned supported RegExp corpus is clean without sending unsupported syntax to
the native backend, and all public RegExp operations consume the same conforming match data.

## Track 3 — Module execution semantics

**Status: the module-binding defects this track has fixed are landed upstream and recorded in
[closed](broiler-js-gaps-closed.md#track-3--module-binding-semantics) — the pinned `Broiler.JS`
gitlink carries them, so CI sees them. One larger defect of the same family — imports are not live
bindings — is characterized but not fixed, because it is an architectural change to how the engine
links modules. JSON modules, `import.meta` and attribute enforcement are now decided, implemented
and live — all three in
[closed](broiler-js-gaps-closed.md#track-3--module-binding-semantics), all three carried by the
pinned gitlink. The rest of track 3 — the host task model, and the `import defer` /
`import.meta.resolve` decisions — stays in
[open](broiler-js-gaps-open.md#track-3--scripts-tasks-and-modules).**

### Newly characterized — not yet fixed

- **Imports are snapshots, not live bindings.** `import { count } from 'm'` copies `m`'s current
  export *value* into a local at import time; a later mutation of `count` inside `m` (through an
  exported mutator) is not seen by the importer — `import { count, inc } from 'm'; inc(); inc();
  count` stays the import-time value instead of tracking the exporter's variable, and the same holds
  for a namespace access `ns.count`. This is the run-time-copy export model: `export let count`
  writes `exports.count = <value>` at its textual position rather than exposing the live binding.
  Two consequences fall out of the same cause: a **cycle** where module `b`, imported while `a`'s
  body is mid-run, reads a hoisted function export of `a` sees `undefined` (the value has not been
  written to `a`'s `exports` yet — function exports are not hoisted into the namespace before bodies
  run); and the immutability fix above lands as a *runtime* read-only error rather than the spec's
  parse-phase SyntaxError. Fixing live bindings is an architectural change — the exporter must expose
  each mutable export as a live accessor over its module-local variable, and every reference to an
  imported name must read through the namespace on each access rather than a one-time local copy —
  so it is scoped here, not attempted as part of these patches. Reproductions: `LiveBinding`,
  `LiveNamespace`, `Cycle` shapes (characterized against the current pointer; no regression is
  committed for them yet, per the retest rule, until the fix is designed).

### Remaining work

1. Re-measure the module corpus against the landed fixes. Track 0's run recorded "22
   NullReferenceException crashes in module namespace and ambiguous-export paths" and "52 that hang";
   the scope-isolation fix targets a distinct read-only-write crash class (not those NREs), so its
   effect on those totals is unknown until the corpus is re-run — do not assume it moves them.
2. Design and implement live import bindings (the *Newly characterized* item). This is the
   architectural piece the exit gate turns on, and it also converts the import-immutability error to
   the spec's parse phase; it is not a patch-sized change.

**Exit gate:** a module's top-level bindings are isolated per module under transitive and cyclic
imports; `export *` obeys local/indirect/star precedence; imported bindings are immutable *and* live
(a reference tracks the exporter's variable, and a hoisted function export is visible across a
cycle); the module corpus is re-run on a pointer that carries the fixes and its manifest reconciled.
