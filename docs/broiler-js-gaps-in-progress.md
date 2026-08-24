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
     track 3;
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
