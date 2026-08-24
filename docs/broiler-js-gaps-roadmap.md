# Broiler.JS gaps roadmap

- **Status:** Active
- **Scope:** Missing, incomplete, unsupported, or observably incorrect JavaScript behavior
- **Last reconciled:** 2026-08-24
- **Evidence basis:** Repository-wide Markdown audit plus the current component revisions

This document consolidates JavaScript gaps recorded anywhere in the Broiler repository, not
only under `Broiler.JS`. It therefore includes core ECMAScript behavior and JavaScript-visible
host, DOM, CSSOM, SVG, worker, and browser APIs. The implementation owner may be Broiler.JS,
HtmlBridge, Broiler.DOM, Broiler.CSS, or another component.

Execution speed, allocation, startup, tiering, caching, boxing, benchmark scores, and other
performance-only work are out of scope. Web Performance API defects remain in scope when the
problem is missing or incorrect observable behavior rather than speed.

This is a coordination roadmap, not a replacement for the owning documents or current failure
manifests. Where this document and an older investigation disagree, the current known-gap test,
current failure manifest, current component roadmap, and current source revision take priority.

## Status and closure rules

- **Confirmed gap:** currently reproduced, present in a current failure manifest, or retained by
  an explicit known-gap regression.
- **Coverage gap:** the runner or host cannot yet provide trustworthy conformance evidence.
- **Capability decision:** an absent platform surface that needs an explicit implement, defer, or
  unsupported-product decision.
- **Retest:** suspected, historical, or unreproduced behavior that is not asserted as a current
  defect.
- **Deliberate exclusion:** a documented profile or product boundary; it is not a Full-profile
  engine defect unless Broiler advertises the excluded capability.

A confirmed gap closes only when:

1. a minimal repository regression fails before and passes after the change;
2. the focused pinned Test262 or WPT path and the affected full shard pass;
3. the failure is removed from its manifest only after CI confirmation;
4. unsupported cases continue to fail deterministically rather than partially succeeding; and
5. the owning status document and publishable compliance evidence are reconciled together.

## Sources of truth

- [Broiler.JS known compliance gaps](../Broiler.JS/docs/compliance/known-gaps.md)
- [Broiler.JS component roadmap](../Broiler.JS/docs/roadmap/Component.md)
- [Broiler.JS compliance dashboard](../Broiler.JS/docs/compliance/dashboard.md)
- [Broiler.Regex implementation status](../Broiler.JS/Broiler.Regex/Broiler.Regex/README.md)
- [Broiler.Regex roadmap](../Broiler.JS/Broiler.Regex/docs/roadmap.md)
- [JavaScript concurrency plan](../Broiler.JS/docs/roadmap/Concurrency.md) and
  [status](../Broiler.JS/docs/roadmap/Concurrency.status.md)
- [Privacy-page API inventory](privacy-test-page-gaps.md)
- [HTML5 JavaScript exceptions](html5test-exceptions.md)
- [Open WPT rendering and API gaps](wpt-rendering-gaps-open.md)
- [Current xUnit suite status](xunit-suite-status.md)
- [DOM bridge roadmap](../Broiler.DOM/docs/roadmap.md)

Do not copy changing pass/fail totals into this roadmap. Link the exact result artifact or update
the dashboard instead.

## Roadmap summary

| Order | Track | Current state | Required outcome |
|---:|---|---|---|
| 0 | Conformance evidence | Coverage gaps closed; pinned-corpus CI run outstanding | Test failures and timeouts are trustworthy |
| 1 | Core language and built-ins | Confirmed gaps | Supported Test262 language clusters are clean |
| 2 | RegExp | Partial implementation | ECMAScript syntax and matching semantics use a complete backend |
| 3 | Scripts, tasks, and modules | Partial host semantics | Parsing and task ordering match observable browser behavior |
| 4 | Workers and shared memory | Worker first slice; shared memory not started | Claimed agent capabilities are complete and deterministic |
| 5 | Essential browser JavaScript APIs | Mixed partial, absent, and stubbed surfaces | A tested support matrix replaces accidental omissions |
| 6 | DOM, CSSOM, and SVG from JavaScript | Partial object and tree models | Script-visible objects and algorithms meet their claimed standards |
| 7 | Graphics, media, and advanced APIs | Large capability decisions | Each surface is implemented or explicitly excluded |
| 8 | Portable/Native-AOT profile | Numeric seed only | Optional profile decision and, if approved, a truthful capability set |

Tracks 1 and 2 can proceed in parallel once track 0 makes their results trustworthy. Tracks 3
through 7 share host and DOM dependencies and must use one published support matrix rather than
silently exposing partial globals.

## Track 0 — Restore trustworthy conformance evidence

**Status: the coverage gaps below are closed; the remaining work is a full pinned-corpus
run in CI and the product decisions for three `$262` hooks.**

### What changed

- **Async results follow test262's marker protocol.** `$DONE` is upstream
  `doneprintHandle.js`, injected into every `flags: [async]` test, and it prints
  `Test262:AsyncTestComplete` / `Test262:AsyncTestFailure:`. No marker, two markers, or a
  failure marker are each a failure with the kind recorded (`asyncCompletion`); a test that
  neither settles nor returns is ended by the per-test timeout. Measured correction over a
  seeded 400-file sample of the 5487 script-goal async files: 10.3% of async results were
  passes that are not passes (~560 across the corpus).
- **Fixtures that must fail.** `Broiler.JS/scripts/compliance/fixtures/async-protocol/`
  holds deliberately failing, rejecting, never-settling, double-completing,
  dying-after-completing and never-returning tests with the verdict each must produce;
  `run_test262.py --self-check` enforces them, and every CI shard runs it first.
- **`module` and `raw` are executed, not skipped.** A module test runs in place under
  `--module-host` with its harness preloaded as a script (so `assert` and `$DONE` are
  globals its body and its `_FIXTURE.js` imports can see); a raw test is handed the file's
  own unmodified bytes. 824 module and 30 raw files were previously reported as skipped.
- **`$262` is defined** for `global`, `createRealm`, `detachArrayBuffer`, `evalScript` and
  `gc`, and a test is excluded for the exact hook it needs and lacks rather than for
  mentioning `$262` — 640 more files now run.
- **An uncaught error is reported by its JavaScript name** (`Uncaught SyntaxError: …`), so
  `negative: phase: parse` tests are matched on the type they raise instead of failing on
  the diagnostic while rejecting the program correctly.
- **Per-mode totals** (selected, executed, passed, failed, skipped, timed out) ride on every
  shard report, survive the shard merge, and appear in both CI summaries.
- **Two Broiler.JS xUnit suites could not run at all**, which is the same kind of untrustworthy
  evidence as a test that cannot fail. Every `Modules.Tests` case that compiles script, and most
  of `Core.Tests`, threw `No compilation back end is registered for 'DynamicMethod'` — while the
  assembly implementing it sat in each suite's own output directory. The DynamicMethod and
  CollectibleAssembly back ends register themselves from a `[ModuleInitializer]`, and .NET loads
  assemblies lazily, so that ran only if the host happened to touch a type in the emitter
  assembly: registration followed incidental load order rather than configuration (a
  `ProjectReference` does not fix it — a reference is not a load). `LinqExpressions`, the
  assembly that genuinely needs the emitter and is loaded while a compilation's tree is being
  built, now forces that load, the same remedy `CompilerAssemblyInitializer` already documents
  for its own assembly. `Modules.Tests` went 3/13 → 13/13 and `Core.Tests` to 32/32, with every
  other suite unchanged and `Portable.Tests` — the profile that prohibits dynamic code emission —
  still green.

See [the host-coverage inventory](../Broiler.JS/docs/compliance/known-gaps.md#host-coverage-gaps)
and [the component host-mode plan](../Broiler.JS/docs/roadmap/Component.md#2-expand-host-mode-coverage).

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

## Track 1 — Core language and built-in correctness

### Direct eval, parser, scope, and control flow

Each bullet below was reproduced against the pinned ref before being worked on or retired;
the ones marked **fixed** carry a minimal regression in
`Broiler.JavaScript.Integration.Tests/Track1LanguageTests.cs`.

- A captured read after deleting an eval-introduced `var` retains the torn-down cell instead of
  re-resolving the name outward. **Fixed:** a sloppy direct eval's `var` is a deletable binding
  of the caller's variable environment, and when a nested closure captures the name the compiler
  binds the function and the closure to one shared `EvalShadowVariable`. `delete` of such a name
  was constant-folded to `false` (the delete-of-identifier path read a non-deletable captured
  binding as a genuine local), so the binding survived and every later read — the function's own
  and the closure's — still saw the eval's value. An eval-shadow binding's deletability is not
  statically known, so `delete` now defers to `JSContext.DeleteIdentifier`, which resolves the
  shadow and tears down only an owned one (giving up ownership so the name forwards to the outer
  binding again). Landed in the pinned `Broiler.JS` submodule (`2b8ef06`); regressions in
  `Track1LanguageTests`.
- An empty statement does not correctly terminate a Directive Prologue. **Fixed:** a statement
  that already ended at its own `;` was consuming a second one, deleting exactly the empty
  statement that ends the prologue.
- A top-level `for await` fails with an internal error instead of running — the "invalid
  program" cluster track 0 measured in the module corpus. **Fixed:** a program holding a
  top-level `AwaitExpression` is flagged async by the parser, and that flag is what routes it
  through the generator rewrite that implements suspension. The `for await` head only
  *validated* that the construct was legal at that position and never set the flag, so the
  program was compiled straight to IL and the await its desugaring emits reached
  `ILCodeGenerator.VisitYield` as a raw yield node it cannot emit (a leaked
  `NotImplementedException`). The head now sets the same flag `AwaitExpression` does when the
  loop is at function depth 0; `for await` where no await is allowed — an ordinary script's top
  level, a non-async function body — stays the SyntaxError it was. Landed in the pinned
  `Broiler.JS` submodule (`98db1fc`); regressions in `Track1LanguageTests`.
- A parameter named `undefined` does not shadow the global binding. **Fixed:** the compiler
  folded every identifier of that name to the undefined value; it now folds only a reference
  that resolves to no binding.
- Async and generator bodies do not enter the runtime strict-mode scope, so a failed strict
  `[[Set]]` may not throw. **Fixed:** such a body runs during its rewritten driver's steps,
  not during the `InvokeFunction` call that created it, so it never inherited the
  `EnterStrictMode` scope ordinary calls establish — a failing `[[Set]]` in a `'use strict'`
  async/generator body silently did nothing (even in the async synchronous prefix / before a
  generator's first `yield`), and a strict async function's `this` was the global object.
  `ClrGeneratorV2` now re-enters the function's own strict flag around each body step, and the
  compiler sets `IsStrictMode` (and `coerceThis`) on the generator and the async inner
  generator. Landed in the pinned `Broiler.JS` submodule (`f1b78df`); regressions in
  `Track1LanguageTests` and the updated `StrictModeFlowTests`.
- **Early errors that never fire** — the cluster the modes and the named-error reporting made
  visible, and the largest one here: a body `var` shadowing the head's lexical name, a labelled
  function declaration as a loop body, `export` in non-module code, and a `var` colliding with a
  lexical binding of the same name were accepted and RUN (or, for `export default <expr>`,
  crashed with a `NullReferenceException`) instead of reaching each test's `$DONOTEVALUATE()`.
  About 30 files across `test/language/statements/{for,if,labeled}`, `test/language/eval-code`
  and `test/language/global-code`. **Fixed** (three mechanisms):
  - **VarDeclaredNames ∩ LexicallyDeclaredNames must be empty at every scope.** The collision
    was detected only order-dependently and lost once a `var` hoisted out of the block it was
    written in, so `var x; let x;`, `let x; { var x; }`, `for (let x of []) { var x; }`,
    `try {} catch ([e]) { var e; }` and their siblings ran with the later declaration winning.
    Each `FastScopeItem` now records its own VarDeclaredNames as a `var` hoists through it, and
    rejects both a `var` hoisting into a lexical binding and a lexical declared where a `var`
    has hoisted through — at every scope, in either order, and across a for-head, switch, or
    destructured catch parameter (a block-nested function declaration counts as lexical, so it
    conflicts with a same-named `var` while two sloppy block functions of one name still
    coexist).
  - **A labelled function declaration is never a legal loop body.** A `let`/`const`
    for-in/for-of/C-style head rewrites its body into a synthetic per-iteration block, hiding
    the labelled statement from validation; the loop parser now rejects it on the statement as
    written, before that rewrite.
  - **`export` in script code is an early error**, not a crash: its bindings target the
    host-injected module `exports` object, absent in a plain script, so every export form now
    raises a clean `SyntaxError`.
  - **A direct eval's global `var`/function may not collide with a global lexical.** The
    var/lexical mechanism above catches a collision with a function- or block-local lexical
    (the direct-eval validator carries those names at compile time), but a top-level
    `let`/`const`/class lives in the runtime global lexical environment and was not among them.
    `JSContext.Register` rejected the collision for an indirect eval, yet a direct eval binds the
    name as a captured lexical and skips that registration, so `let g = 1; eval('var g;')` ran
    with the eval's `var` aliasing the lexical. The compiler now emits
    `EnsureNoGlobalLexicalConflictForEvalVar` in the eval's hoisting prelude for a
    skip-registration program-scope var/function, covering `let`/`const`/class, a function
    declaration, a non-first declarator and a block-hoisted var, in both direct and indirect eval.

  The first three mechanisms landed in the pinned `Broiler.JS` submodule (`f1b78df`); the
  global-lexical direct-eval case in `943b94d`. Regressions (and the accept-guards for valid
  neighbours) in `Track1LanguageTests`; the `BuiltInsTests` that pinned the old shadowing
  behaviour now assert the SyntaxError.
- `new.target` is rejected in a direct eval nested inside an eval-compiled function.
  **Does not reproduce**; `staging/sm/class/newTargetEval.js` passes. It is in the failure
  manifest from an older run — confirm against a current CI run before removing the path.
- Comment and regular-expression-literal lexical edge cases, labeled and unlabeled `continue`,
  and block-scoped loop bindings. **Do not reproduce:** `test/language/comments`,
  `test/language/asi` and the `for`/`if`/`while`/`do-while`/`labeled` directories pass apart
  from the early-error cluster above.
- Remaining Annex B cases must be reduced from the current manifest rather than reconstructed
  from deleted issue snapshots.

Evidence:

- [Active semantic clusters](../Broiler.JS/docs/compliance/known-gaps.md#active-semantic-clusters)
- [Current component failure clusters](../Broiler.JS/docs/roadmap/Component.md#1-close-the-supported-test262-failure-set)
- [Parameter-shadowing record](../Broiler.JS/docs/roadmap/Phase-3.status.md)
- [Strict async/generator record](../Broiler.JS/docs/roadmap/Archive.md), retained by
  [Measurement.md](../Broiler.JS/docs/roadmap/Measurement.md)

### Objects, arrays, symbols, and Proxy-sensitive behavior

- Symbol own keys enumerate by Symbol-creation order rather than property-creation order.
  **Fixed:** it was a property-storage gap, not a sort to delete — symbol properties lived in
  a hash map keyed by the symbol's creation id, which records no insertion order, so
  `getOwnPropertySymbols` sorted by creation id, `Reflect.ownKeys` used raw hash order, and
  `Object.assign` copied in hash order, all disagreeing. Each object now records its symbol
  keys' insertion order (as `PropertySequence` does for string keys — appended on first add,
  dropped on delete, re-added at the end, position kept on update), and every enumeration path
  reads it through `JSObject.SymbolsInInsertionOrder`. Landed in the pinned `Broiler.JS`
  submodule (`f1b78df`); regressions in `Track1LanguageTests`.
- `slice`, `unshift`, `toReversed`, `reduceRight`, array mutation limits, near-maximum lengths,
  and Proxy-created results retain confirmed failure paths. **Largely does not reproduce:** all
  four directories pass except `slice/create-proto-from-ctor-realm-array.js`, a cross-realm
  species case.
- A constructor's cached `prototype` and its observable `prototype` property could disagree, so
  `new f()` built instances on an object the property never held. **Fixed** (arrived from the
  retest queue, where it was recorded as a suspected "rejected `prototype` write changes later
  `[[Construct]]`"; it reproduced, and turned out to be two defects pointing opposite ways).
  `[[Construct]]` reads a cached field rather than re-reading the property, and: the indexer
  cached the value a write *attempted*, before the store and with no success test — but the write
  is rejected outright when `prototype` is non-writable (a class constructor, a frozen function,
  an explicit `writable: false`), so a *refused* change took effect for `new` while the property
  kept its old object, and in strict mode the write threw and still took effect; and
  `DefineProperty` tested its result for a truthy value, where `[[DefineOwnProperty]]` reports
  success as `undefined` and failure as `false`, so every *accepted* `defineProperty` was read as
  a failure and `new` ignored it. Both paths now re-cache from what the property currently holds.
  `Reflect.construct`, which reads the property, was correct throughout and disagreed with `new`
  on the same function — the regressions assert the two now agree. Landed in the pinned
  `Broiler.JS` submodule (`3d8b456`); regressions in `FunctionPrototypeWriteConstructTests`.
- `Reflect.set(base, key, value, receiver)` gives a new receiver property the base property's
  attributes instead of the all-true descriptor required by `CreateDataProperty`. **Fixed:**
  the receiver-create paths in `JSObject.PropertyStorage` use the CreateDataProperty
  attributes. No test262 file at the pinned ref reaches the case, so
  `ReflectSetReceiverAttributesTests` — which pinned the deviation and now pins the fix — is
  the only evidence there is.

Evidence:

- [Symbol and array gaps](../Broiler.JS/docs/compliance/known-gaps.md#active-semantic-clusters)
- [Reflect.set known deviation](../Broiler.JS/docs/roadmap/Phase-2.status.md)

### Actions

1. Add or retain one minimal observable-value or expected-error regression for every bullet.
2. Fix parser, compiler, environment, property-storage, or built-in ownership separately; avoid
   broad fixes that make the failing suite path impossible to attribute.
3. Run the focused cluster, affected full shard, and cross-feature cases involving Proxy,
   species, accessors, strict mode, and realm boundaries.
4. Remove failure-manifest paths only after the pinned CI run confirms the change.

**Exit gate:** supported parser, eval, scope, control-flow, Array, Reflect, Symbol, property-order,
and Proxy-sensitive Test262 clusters contain no unexpected failures. Deliberate deviations are
documented separately and do not appear as ordinary failures.

## Track 2 — Complete ECMAScript RegExp behavior

**Status: the Broiler.Regex engine gaps (actions 1–3, 5) are closed; the remaining work is
the one match-data abstraction that `Split` and `Replace` need (action 4), the pinned CI
run, and the translator retirement (action 6) that depends on both.**

### What changed

Each bullet below was the state of `Broiler.Regex` before this work; the fix lives in the
submodule and carries a minimal regression in `Broiler.Regex.Tests`.

- **`\p{...}` / `\P{...}` resolved.** `UnicodeCharSets.ResolveProperty` reads the pinned
  `Broiler.Unicode` range tables — the same generated UCD revision the JavaScript layer's
  translator uses, so the two cannot disagree about a property's members. General_Category
  (lone value and the `gc=` dimension), binary properties with their ECMAScript aliases,
  `Script`, `Script_Extensions`, and the UTS #51 properties of strings all resolve; an
  unknown name is a located `RegexSyntaxException` rather than a mis-match. Regressions:
  `UnicodePropertyEscapeTests`.
- **`v`-mode class sets evaluated, not just parsed.** Nesting, `&&`, `--` and `\q{…}`
  string alternatives are evaluated against a new `CodePointSet` with real set algebra, and
  a class holding multi-code-point members compiles to a longest-first alternation in the
  matcher. The ordering that matters is §22.2.1 MaybeSimpleCaseFolding *before* §22.2.2.9
  CharacterComplement, whose universe under `vi` is only the code points that fold to
  themselves — that is the entire reason `[^\p{Lu}]` matches neither `A` nor `a` under
  `vi` while it matches `a` under `ui`. Regressions: `UnicodeSetsTests`.
- **`Canonicalize` is table-driven.** Both branches now come from the Unicode Character
  Database (`Unicode/Generated/CaseFoldingData.g.cs`, regenerated by
  `Unicode/tools/generate-case-folding.py`) instead of `char`/`Rune` casing plus five
  hand-written special cases. Two consequences are observable: `İ` and `ı` have no simple
  case folding, so neither is case-equivalent to an ASCII `i` any more — the old
  `ToLowerInvariant` derivation folded both to `i`; and §22.2.2.9.2 WordCharacters now
  widens `\w` under `iu` from the fold table, so `/\W/iu` correctly rejects `ſ`.
  Regressions: `CanonicalizeTests`.
- **The Annex B / Unicode-mode grammar split.** Six boundaries, all found by differential
  testing rather than by the roadmap: `\u{…}` was being read as a code point without the
  `u` flag (it is the identity escape `u` there); a bare `]`/`{`/`}` was a literal in
  Unicode mode; a class escape was accepted as a range endpoint in Unicode mode; `\-` was
  rejected inside a class; a quantifier was silently dropped after every assertion, where
  Annex B admits one after a look-ahead (`/(?!a)+/`) and forbids it everywhere else
  (`/^{2}/`, `/(?<=a)*/`); and `\k<n>` was a broken reference in a pattern that declares
  no group name, where it is the literal text `k<n>`. Regressions: `AnnexBGrammarTests`.
- **Routing expanded (action 5).** `JSRegExp`'s gap scan now routes `v`-mode class-set
  expressions and property escapes used as class members — the two shapes the translator
  has to approximate, since it evaluates the set itself and can only fall back to .NET's
  code-unit-based `\p{Lu}` inside `[…]`. Property escapes reaching the parser without
  throwing also means a pattern such as `(?<=(\p{L}))b`, refused before, now routes.
- **One match-data abstraction (action 4).** `exec` already read either backend through
  `RunMatch`; now `Split` and `Replace` do too, via a shared `EnumerateMatches` iterator
  over `RunMatch` (code-point-aware empty-match advance, `lastIndex` left untouched exactly
  as the .NET path left it). So the legacy `String.prototype.split`/`replace` fallback —
  reached when a RegExp's `@@split`/`@@replace` is removed — answers a routed gap pattern
  with Broiler's match data instead of the .NET translation's, which is wrong for exactly
  those patterns (e.g. `(a?b??)*` on "ab" matches "ab", not "a"). `IJSRegExp.Value`, which
  handed callers the raw .NET `Regex` (and would be `null` once a Broiler-only pattern is
  routed), is retired for an engine-agnostic `IsMatch`, so `assert.match` routes too.
  Regressions: `BuiltIns.Tests/RegExpEngineConsistencyTests`.
- **A routed pattern no longer needs a .NET translation (the tail of action 4).**
  `CreateRegex` compiled the .NET `Regex` for every pattern, so one the translator could not
  represent failed to construct even though Broiler would match it. Now that no supported
  operation reads the .NET engine for a routed pattern, the translation is wrapped so that,
  *for a routed pattern only*, a transform's "not supported" error or a `new Regex` rejection
  falls back to a null `value` and the RegExp runs on Broiler alone. A `v`-mode set operation
  over a built-in class escape — `[\s&&\S]`, `[\d&&\s]`, `[\s--\d]` — is the concrete case:
  valid ECMAScript that the .NET UnicodeSets translator rejects, and that now constructs and
  matches (exec/test/split/replace) in agreement with V8. A pattern invalid in *both* engines
  is not routed (Broiler rejects it, so the fallback is skipped) and still throws.
  Regressions: `BuiltIns.Tests/RegExpTranslatorFallbackTests`.
- **Routing widened to all of Unicode mode (action 6, first tranche).** Routing no longer
  triggers only on the gap *shapes*: every `u`/`v` pattern Broiler can build now runs on
  Broiler, because Unicode mode is where the .NET translation is most complex and fragile.
  A JSRegExp-level differential against V8 found the bugs this fixes — a standalone `\p{…}`
  under `i` **threw** a SyntaxError, and in-class case folding was missed (`[α-ω]/iu` did
  not match `µ`, which folds to `µ`→`μ`). Non-Unicode patterns stay on the mature, far
  faster .NET engine (the common ASCII/hot-loop case) until the matcher is optimized.
  Regressions: `BuiltIns.Tests/RegExpUnicodeRoutingTests`.
- **Single-code-point quantifiers repeat iteratively (action 6, second tranche).** The
  continuation-passing matcher recurses once per quantifier iteration, so a repeat over a
  long subject would overflow the stack. A quantifier whose body consumes exactly one code
  point — a literal, `.`, or a character class — is now repeated by an iterative fast path
  (a linear scan with an index-based backtrack, provably equivalent to RepeatMatcher for
  such a body), so `.*`, `\d+` and `[^"]*` match a subject of any length natively. A body
  the fast path cannot take (a capturing group, an alternation of sequences) still recurses;
  there the matcher checks the stack and throws `RegexOverflowException` instead of crashing,
  and `RunMatch` catches it and falls back to the iterative .NET engine for that subject.
  Regressions: `Broiler.Regex.Tests/StackDepthGuardTests` and
  `BuiltIns.Tests/RegExpUnicodeRoutingTests`.
- **Complex-body quantifiers repeat iteratively too (action 6, third tranche).** A body the
  fast path cannot take — a capturing group, an alternation of sequences — no longer recurses
  per iteration: it runs through an explicit-stack RepeatMatcher where each iteration level is
  a heap frame and each level's body matches are enumerated in the body's own backtracking
  order (by re-running the body with a continuation that fails N times then succeeds). Greedy
  tries every body-match subtree before the stop branch, lazy the reverse, and the
  empty-iteration guard is preserved — exactly what the recursive form computed, with only the
  iteration dimension moved off the call stack. So a repeat over a subject of any length now
  matches natively for *every* body shape. Native recursion is bounded by the pattern's
  nesting depth (like the recursive-descent parser that accepted the pattern), never by the
  subject, so the `RegexOverflowException` backstop is no longer reachable by input size — it
  remains only for a pathologically nested pattern. Regressions:
  `Broiler.Regex.Tests/StackDepthGuardTests` and `BuiltIns.Tests/RegExpUnicodeRoutingTests`.
- **A first matcher-performance pass, ahead of non-Unicode routing (action 6, fourth
  tranche).** Routing the non-Unicode hot path was measured against the compiled .NET engine
  first: the interpreter was ~10× slower on average (up to 24× for `\d`/`\w`-heavy patterns),
  which would be a large regression for no correctness gain — so, as the roadmap's own
  ordering requires, the routing flip is deferred and the performance work comes first. A
  first allocation pass is done and measured: `MatchState` is a `readonly struct` (deriving a
  state per code point no longer allocates); the single-code-point quantifier fast path scans
  and backtracks with no per-iteration list; one budget and one capture array are reused
  across a run's start positions; and a run of ≥2 single-code-point atoms in a forward
  sequence folds into one loop rather than a continuation closure per term. That takes simple
  patterns (`\d+`, `[a-z]+`, `\w+`, class scans) to ~2–3× the compiled .NET engine and the
  microbenchmark mean from ~10× to ~6×, with identical results. What remains is structural —
  a sequence with capturing groups or nested quantifiers still allocates a continuation-closure
  chain per invocation, and each capture write clones the capture array — and needs the
  compiled/bytecode path (defunctionalised continuations, explicit backtrack stack), which is
  what makes non-Unicode routing viable.

Evidence: `dotnet test Broiler.Regex.slnx` — 253 tests, up from 57; the Broiler.JS
`BuiltIns` (2214) and `Integration` (5024 of 5025) suites pass, the one failure being the
pre-existing `M8_DocumentationFiles_Exist`, which asserts a `docs/roadmap.md` that the
component's own reorganisation replaced with `docs/roadmap/`. Plus 220,172 differential
Broiler.Regex cases and a 12,792-case JSRegExp-level differential — both against V8 — whose
only remaining mismatches are the two documented deliberate `vi` divergences, a further
1,534 quantifier-focused cases for the single-char fast path, 3,008 complex-body cases
for the general driver (nested quantifiers, empty-capable bodies, backreferences, lazy,
multi-capture, look-behind bodies), and 2,478 cases re-run after the allocation pass
(struct state, fast path, per-start reuse, atom-run folding) — all against V8 with zero
mismatches.

The engine and routing changes have landed in the pinned submodules: the `Broiler.Regex`
work reached `4df3fb8` and the `Broiler.JS` routing reached `d20e506`, so their patch files
were deleted and the gitlinks point at commits that contain them. See
[`patches/README.md`](../patches/README.md) for the current (now empty) patch backlog.

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

## Track 3 — Scripts, tasks, and modules

### Module syntax

- The `export { … }` clause was rejected in every form, so the commonest way to export — declare
  a binding, then publish it — did not work: `const x = 1; export { x }` failed as "x is already
  defined in current scope", `var x = 1; export { x }` as "Expecting keyword from",
  `export { a } from './m.js'` as "Cannot convert undefined or null", and `export * from './m.js'`
  with a `NullReferenceException`. Only `export <declaration>` and `export default` worked.
  **Fixed:** the parser had no ExportClause production at all — it read the braces as an object
  *destructuring pattern* that declared each name as a `var` and then demanded a `from`, which is
  exactly why a `const`/`let` collided while a `var` fell through to the missing keyword. A clause
  is not a declaration. There is now a real clause reader (the mirror of the import side, which the
  grammar makes symmetric), specifier pairs on `AstExportStatement`, and a compiler path that
  publishes each local binding onto `exports` — or, with `from`, imports the source once and copies
  the specifiers off it. Reserved words follow the grammar rather than a special case: a
  ModuleExportName is an IdentifierName, so any reserved word is legal after `as`, while the local
  name of a clause without `from` is an IdentifierReference and may not be one. Exporting an
  undeclared name is now an early error naming it. Landed in the pinned `Broiler.JS` submodule
  (`51e3830`); regressions in `ExportClauseTests`, which assert the exported value reaches an
  importer rather than only that the source parses.
- `export * from './m.js'` — the form barrel files are built on — was unimplemented and crashed
  with a `NullReferenceException` (it carries no declaration, so it reached a switch on
  `Declaration.Type`). **Fixed:** unlike `export { a } from`, its names cannot be emitted specifier
  by specifier, because which names exist is a property of the *source* module rather than of this
  module's text — so the module is imported once and its own enumerable keys are republished at run
  time. `default` is excluded, per the star entry's `all-but-default` `[[ImportName]]`, which is
  what stops a barrel file re-exporting several modules from having their defaults collide. The
  parser's bare-star branch also never set `isAsync`, latent while the form could not compile at
  all and load-bearing once the emitted code awaits the import. Landed in `7835604`; regressions in
  `ExportClauseTests` cover republication, namespace visibility, the excluded default, and
  composition with the module's own exports. (`export * as ns from` is a separate production and
  already worked.)

- The module duplicate-name early errors (§16.2.1.5) never fired, so `import { a, a } from 'm'`,
  `import { a } from 'm'; var a;`, two imports binding one name, and a second `export default` all
  ran with one binding quietly winning. **Fixed:** a collision between an ImportedBinding and a
  top-level `let`/`const`/class was already caught by the scope machinery, and that masked the
  rest. ImportedBoundNames are now collected across the module's imports, rejected on duplication,
  and intersected with the hoisting scope — the right source for VarDeclaredNames, since a `var`
  in a nested block still declares a name at the module's top level. For exports the rule
  implemented is the general one (ExportedNames must contain no duplicates) rather than a narrow
  "one default export" check: `default` is just an exported name, so the same rule catches
  `export { x as default }` beside an `export default`, and `export { x }; export { y as x }`
  besides. None of it needs the module parse goal — an ImportDeclaration is only legal in module
  code, so the check is inert for a script. Landed in `fdbc2c0`; regressions in
  `ModuleDuplicateBindingTests`.
- `export class C {}` never worked: the compiler cast the class declaration to
  `AstFunctionExpression`, which is null for a class node, and reading its name threw a
  `NullReferenceException`. **Fixed** in `fdbc2c0` alongside the above.
- Import attributes were rejected where the grammar allows them. `AttributeKey` is
  `IdentifierName | StringLiteral` and only the identifier half was implemented, so
  `with { "type": "json" }` — the quoted form the proposal's own examples use — was an unexpected
  token; and an `ExportDeclaration` with a `FromClause` takes a `WithClause` exactly as an
  `ImportDeclaration` does, but none of the three export `from` forms accepted one. **Fixed** in
  `2f226ef`; regressions in `ModuleAttributeClauseTests`. Note this fixes valid source failing to
  compile, not attribute *enforcement*: attributes are parsed and not acted on, as they already
  were for imports (nothing reads `AstImportStatement.Attributes`). Rejecting a module whose type
  does not match its attribute is a separate capability decision.
- **Still open — a JSON module's default import is `undefined`.** `import d from './data.json'`
  yields `undefined`, so JSON modules are effectively unusable. The module host wraps a `.json`
  file as `module.exports = <json>`, which replaces the exports object with the parsed value, and
  a default import then reads `.default` off that value and finds nothing. Per ES2025 a JSON
  module has exactly one export, `default`, and no named exports — but this engine serves both
  `require` (which wants the object itself) and `import` from the same wrapper, so making the two
  agree is a product decision about the CommonJS/ESM boundary rather than a mechanical fix, and is
  left for one. Characterized, not guessed at.
- **Still open — `import.meta`** reports "import.meta not supported" (deterministic, not a crash),
  and `import defer` (stage 3) is not parsed. Both are capability decisions rather than defects.
- **Still open — needs a module parse goal.** `await` as a module-level identifier (`var await`,
  `function await`, `let await`, a parameter, a class name, `import { a as await }`) is accepted;
  it is reserved from a module's first token, before any import or export is seen, so it cannot be
  inferred from the AST the way the duplicate rules can. Module-ness is currently expressed only by
  the CommonJS-style `argsList` wrapper the module host passes to `CoreScript.Compile`, so a real
  goal would have to be threaded through `JSCode`/the code-cache key as well — a script and a
  module with identical text must not share a compile.

### Confirmed host-semantic gaps

- Parser-blocking scripts execute after the complete document is parsed, so they cannot observe
  or mutate the correct mid-parse DOM. See [WPT reftest status](wpt-reftests.md).
- Deferred and module scripts, timers, rendering tasks, and microtask checkpoints use fixed phase
  buckets instead of one ordered task model. See [HtmlBridge architecture](architecture/htmlbridge.md).
- A subdocument module can start without deadlocking but its continuation and DOM effect are not
  drained in the engine-only module path. See [xUnit status](xunit-suite-status.md).
- `document.currentScript` remains approximate for unresolved or CSP-blocked sources and hosts
  that hoist async scripts. See [the focused investigation](google-about-current-script.md).

### Actions

1. Execute parser-blocking scripts at the parser checkpoint while preserving `document.write`,
   current-script identity, and error propagation.
2. Define one ordered host task model for classic, deferred, async, and module scripts, timers,
   rendering callbacks, and microtask checkpoints.
3. Give top-level and subdocument module execution an explicit completion/drain contract.
4. Pin ordering fixtures that combine frames, promises, timers, deferred scripts, module graphs,
   failures, and navigation requests.
5. Remove current-script heuristics that associate a blocked or hoisted script with a neighbour.

**Exit gate:** parser-blocking fixtures observe the correct partial DOM; task-order fixtures match
the published model; iframe modules complete their expected DOM effects; current-script identity
is correct for success, failure, deferred, async, module, and blocked-script paths.

## Track 4 — Workers, concurrent contexts, and shared memory

### Worker first-slice gaps

The current Worker slice excludes module workers, `SharedWorker`, nested workers, worker
`requestAnimationFrame`, `MessagePort` transfer, and network-fetched worker scripts. Its lifecycle,
FIFO, cancellation, error, shutdown, failed-transfer atomicity, and explicit shared-memory policy
remain acceptance work. See [Concurrency status](../Broiler.JS/docs/roadmap/Concurrency.status.md)
and [the Worker result](../tests/render-stages/results/worker-object.md).

### Concurrent-context correctness

General concurrent-context safety is not accepted. Mutable inline-cache/site and type-feedback
ownership, every async and host entry, and disposed-context reclamation have not been fully
enumerated and validated.

### Shared memory and Atomics

Cross-agent `SharedArrayBuffer` and Atomics are not implemented. The existing single-agent or
simulated behavior does not establish shared backing-store lifetime, no-tear access, ECMAScript
ordering, atomic read-modify-write operations, waiter lists, `AgentCanSuspend`, or cleanup during
growth and termination.

### Actions

1. Specify the agent lifecycle and every queue, interleaving, error, close, and termination rule.
2. Make transfer-list validation atomic: a later invalid entry must not expose partial detachment.
3. Implement or explicitly reject each excluded Worker capability until its complete gate passes.
4. Enumerate and isolate mutable engine state before advertising general parallel contexts.
5. Treat cross-agent shared memory as a separate capability; keep it unavailable until its full
   ordering, no-tear, RMW, wait/notify, timeout, growth, and termination tests pass.

**Exit gate:** applicable Worker, structured-clone, and Test262 cases pass under deterministic
multi-agent stress; failed transfers are atomic; unsupported capabilities reject explicitly; no
shared-memory claim is made before the complete memory-model gate passes.

## Track 5 — Essential browser JavaScript APIs

### Fetch, navigation, storage, and networking

- `fetch()` returns a self-returning thenable rather than a conforming chainable Promise.
- `location.assign`, `replace`, `reload`, and `href=` record requests but do not navigate.
- Some HTTP subresource, iframe, worker, socket, and navigation attempts never complete or call
  back to the probing script.
- IndexedDB, Cache API, service workers, `cookieStore`, `navigator.storage`, WebSocket,
  EventSource, and `SharedWorker` are absent.

See [the privacy-page gap inventory](privacy-test-page-gaps.md) and
[the Location changelog entry](../CHANGELOG.md).

### Window, document, navigator, URL, and timing semantics

- Navigator identity, hardware, connection, permissions, storage, media-device, media-capability,
  and user-agent-data surfaces remain incomplete.
- Window and screen geometry plus `BarProp` objects are absent.
- `document.hasFocus`, `referrer`, `domain`, `lastModified`, `charset`, `activeElement`,
  `window.trustedTypes`, and `onvisibilitychange` remain unresolved in the current audit.
- Non-special URLs such as `data:` can report an empty `.protocol`.
- `performance.now()` uses a whole-millisecond wall clock rather than a monotonic source, and
  Performance Navigation Timing exposes no timing marks. These are API-semantic gaps, not speed
  work.

See [the privacy inventory](privacy-test-page-gaps.md),
[the Google current-script investigation](google-about-current-script.md), and
[the Google post-consent investigation](google-search-post-consent-challenge.md).

### Actions

1. Replace the fetch thenable with Promise-conforming settlement and chaining while retaining
   correct `await` behavior.
2. Define capture-mode navigation semantics and a complete callback/error contract; do not expose
   browser-like methods whose only observable effect is silent non-navigation.
3. Implement storage and networking APIs in independently testable slices with origin, lifetime,
   failure, and frame/worker behavior pinned from the start.
4. Complete foundational window, document, navigator, URL, screen, and timing properties before
   using broad compatibility pages as acceptance evidence.
5. Publish an API support matrix that distinguishes implemented, negative stub, deliberately
   unsupported, and not-yet-implemented surfaces.

**Exit gate:** Promise chaining, navigation/callback, URL, timing, origin, frame, worker, and
storage fixtures pass for every claimed API; every absent API has an explicit product decision
and deterministic detection behavior.

## Track 6 — DOM, CSSOM, SVG, and script-visible document behavior

### DOM interface and collection model

- DOM wrappers do not consistently use genuine interface/prototype chains.
- `Blob`, `FileList`, `NodeList`, `HTMLCollection`, and per-tag `HTML*Element` constructors remain
  undefined; `childNodes` returns a JavaScript array instead of `NodeList`.
- Qualified mixed-case attributes such as `viewBox`, `preserveAspectRatio`, and `xlink:href` can
  be inaccessible through canonical DOM lookup.
- CharacterData failures are not proper `DOMException` objects.
- `compareDocumentPosition` returns `-1`, `0`, or `1` instead of the required position bitmask.

See [HTML5 exceptions](html5test-exceptions.md) and
[the DOM bridge roadmap](../Broiler.DOM/docs/roadmap.md).

### Custom Elements, templates, and Shadow DOM

- WPT currently relies on a `customElements` runner shim; there is no production implementation.
- `template.content` is a snapshot rather than the parser-owned fragment required by HTML.
- Shadow DOM uses synthetic markers, selector rewriting, and light-child hiding rather than a
  canonical shadow and composed tree with slot assignment, fallback, hit-testing, traversal, and
  event retargeting.

See [the WPT shim record](wpt-rendering-gaps-fixed.md) and
[the root roadmap](ROADMAP.md).

### CSSOM, fonts, SVG, and JS-visible layout algorithms

- A linked stylesheet can report zero `cssRules`, while `getComputedStyle` ignores its
  declarations.
- `getComputedStyle().display` can report `inline` for every element.
- Font Loading is a synchronous compatibility facade and accepts malformed non-empty shorthands.
- SVG lacks conforming live DOM integration for features such as `requiredFeatures` and
  `SVGStringList`; serialized rendering prevents some script mutations and cascade changes from
  reaching paint.
- Current tests retain JS-visible failures involving SVG `elementFromPoint`, writing-mode
  `scrollIntoView`, keyframes read from style text, scroll clamping, and mutated iframe state.

See [open WPT gaps](wpt-rendering-gaps-open.md),
[MediaWiki computed-style evidence](mediawiki-vector-rendering.md),
[the Font Loading changelog entry](../CHANGELOG.md), and
[current xUnit status](xunit-suite-status.md).

### Actions

1. Establish real interface prototypes and Web IDL collection behavior before adding more
   compatibility-only constructor globals.
2. Fix attribute, CharacterData, position-bitmask, range, mutation, and exception semantics with
   focused DOM regressions.
3. Implement production Custom Elements and parser-owned template contents.
4. Replace the synthetic Shadow DOM model with canonical shadow/composed-tree ownership.
5. Make CSSOM rules and computed style read from the same declarations used by cascade and
   rendering.
6. Connect live SVG DOM mutations to cascade and paint.
7. Characterize form dirty/default/reset/radio behavior before promoting it from the retest queue.

**Exit gate:** claimed DOM interfaces have correct prototypes, collections, exceptions, and
algorithms; Custom Elements, templates, shadow/composed trees, CSSOM, computed style, and SVG
mutations pass focused WPT paths without runner-only shims.

## Track 7 — Graphics, media, and advanced Web APIs

These surfaces require product scope decisions before implementation because several are
deliberately absent rather than accidentally broken.

### Canvas and graphics

- Canvas `measureText` is approximate and font-insensitive.
- Affine transforms, `drawImage`, gradients, patterns, clipping, ellipses, line dashes, `Path2D`,
  and `toBlob` are absent.
- The backing bitmap is script-readable but is not painted into the page.
- WebGL, `OffscreenCanvas`, and CSS Paint are absent.

### Media, communications, devices, and security

- Web Audio is absent.
- WebRTC, `RTCDataChannel`, media devices, and `getUserMedia` are absent.
- MediaSource and media playback are negative stubs without a playback pipeline.
- `crypto.subtle` is absent.
- Generic sensors, speech synthesis, and Bluetooth are absent.
- Notifications expose denial behavior because there is no presentation surface.

See [HTML5 JavaScript exceptions](html5test-exceptions.md),
[the privacy API inventory](privacy-test-page-gaps.md),
[open WPT API gaps](wpt-rendering-gaps-open.md), and [the changelog](../CHANGELOG.md).

### Actions

1. Record implement, defer, or unsupported decisions for each surface and for each claimed product
   profile.
2. Complete Canvas 2D and page-paint integration as one coherent capability rather than exposing
   unrelated methods over an invisible bitmap.
3. For every approved API, define security, origin, lifecycle, error, and worker/frame behavior
   before exposing its global.
4. Keep unapproved capabilities absent or explicitly negative; do not add shape-only stubs that
   imply usable functionality.

**Exit gate:** every advertised graphics, media, communications, device, and security API passes
its focused behavior suite; every deferred or unsupported capability is explicit in the support
matrix and produces deterministic feature detection.

## Track 8 — Conditional portable and Native-AOT profile

Broiler.JS currently has no general JavaScript path for environments where dynamic code emission
is prohibited. The portable implementation is a numeric precompiled-bytecode seed and lacks
general JavaScript values, objects, calls, closures, exceptions, modules, eval, async/generators,
and host integration.

This is a conditional capability roadmap, not a defect in the supported Full IL profile. Start it
only after the owning product decision chooses an execution-only, narrow-runtime, full-runtime,
or no-go profile.

See [the Phase 6 plan](../Broiler.JS/docs/roadmap/Phase-6.md),
[current status](../Broiler.JS/docs/roadmap/Phase-6.status.md), and
[public profiles](../Broiler.JS/docs/public-api.md).

**Exit gate:** expected results, Full-profile results, and portable results agree for every
approved capability; every claimed runtime identifier publishes and executes that profile; narrow
profiles publish deterministic exclusions; a no-go decision removes the implied general-JavaScript
claim.

## Retest queue — not yet confirmed as current defects

Do not schedule fixes for these until the smallest current-pointer reproduction exists:

- ~~suspected overlap or offset wrong answers in `TypedArray.prototype.set`~~ — **retired: does
  not reproduce.** `TypedArraySetOverlapTests` (21 cases) is the minimal current-pointer
  reproduction the gate asked for and every case already answers as §23.2.3.26 requires. The
  three that constrain an implementation are the different-element-type overlaps on one buffer,
  where a naive in-place loop reads bytes it has already overwritten: a `Uint16`, `Uint32` and
  `Int16` source copied over an overlapping `Uint8` target each gave the clone-source-first
  answer, not the naive one. Same-type overlap in both directions, offsets (including a
  fractional one), the `RangeError` cases, the offset-before-element-read ordering rule, and
  element conversion are pinned alongside them. The tests stay as the guard for the optional
  fast copy path (MOD-M8-5), which is what would reintroduce the hazard; the
  [gate](../Broiler.JS/docs/roadmap/Component.md#immediate-correctness-gate-typedarrayprototypeset)
  records the full case list;
- ~~older `Intl.DateTimeFormat` range/parts, SameValue, and Proxy-ordering reports~~ —
  **retired: none of the three reproduces**, pinned by `IntlDateTimeFormatRangeTests`. The
  options bag is read in exactly the `InitializeDateTimeFormat` order (observed through a
  `Proxy`: `localeMatcher` through `timeStyle`, 20 keys); `formatToParts` splits a rendering
  into typed parts and `formatRangeToParts` marks the start components `startRange`, the
  separator `shared` and the end components `endRange`; two equal instants collapse to the
  single non-range rendering (`formatRange(d, d) === format(d)`, every part then `shared`); an
  invalid date is a `RangeError` and an undefined endpoint a `TypeError`; and `resolvedOptions`
  returns a fresh object with stable contents. The tests assert structure rather than rendered
  separator text, which is ICU-version dependent;
- ~~historical M0 failures where `JSON.stringify` ignored a `toJSON` result and
  `Array.isArray(new Proxy([], {}))` returned false~~ — **retired: neither reproduces.**
  `toJSON` is honoured for a plain object, a nested value, an array element (receiving its
  index as the key), a `Date`, and a `toJSON` returning an object, and it composes with a
  replacer; `Array.isArray` answers through a proxy, a proxy of a proxy and a non-array proxy,
  throws for a revoked one, and `Object.prototype.toString` and `JSON.stringify` agree with it;
- ~~a rejected function-`prototype` write historically changing later `[[Construct]]`
  behavior~~ — **reproduced and fixed**; moved to
  [track 1](#objects-arrays-symbols-and-proxy-sensitive-behavior);
- ~~the archived observation that async continuations did not run under in-process `Eval` or
  `Execute`~~ — **retired: does not reproduce**, pinned by `AsyncContinuationDrainTests`. A job
  queued by script during an in-process `Eval` — a promise reaction, a chain of them, a
  rejection handler, or an async function's resumption after one or several `await`s — has run
  by the time the call returns, so a later `Eval` on the same context observes its effect; the
  same holds through `EvalWithTopLevelAwaitAsync`. Both halves of the contract are pinned: the
  reaction is not run inline during the script that queues it, and it is not lost either;
- an unreproduced module-initializer-ordering failure — **narrowed, still open.** It is a single
  recorded `ModuleExtensions.Tests` failure whose first test is order-dependent by construction
  ("before the BuiltIns `[ModuleInitializer]` that wires it had run"); 12 further runs are clean,
  which with the 9 already on record is 21 without a recurrence — but the owning record's own
  point stands, that a handful of runs cannot separate a 1-in-10 flake from a 1-in-10 regression,
  so it is not retired. One *related* order dependence has been removed since: compilation
  back ends registered from a `[ModuleInitializer]` that only ran if the host happened to load
  the emitter assembly, which is now forced (below); and
- form-control dirty/default/reset/radio semantics, which remain uncharacterized.

Sources:

- [TypedArray gate](../Broiler.JS/docs/roadmap/Component.md#immediate-correctness-gate-typedarrayprototypeset)
- [Older compliance triage](../Broiler.JS/docs/compliance/known-gaps.md)
- [M0 Test262 subset](../tests/m0-baseline/conformance/test262-subset/test262-subset-summary.md)
- [Historical status reconciliation](../Broiler.JS/docs/roadmap/Roadmap.status.md)
- [Archived async observation](../Broiler.JS/docs/roadmap/Archive.md)
- [Module initialization record](../Broiler.JS/docs/roadmap/Phase-1.status.md)
- [DOM form roadmap](../Broiler.DOM/docs/roadmap.md)

**Retest rule:** add the minimal current-pointer reproduction first. If it reproduces, move it to
the owning track and apply the normal closure gate. If it does not, record the exact cases and
revisions tried, then retire or narrow the note rather than carrying it as an asserted gap.

## Stale and deliberately excluded records

Do not reopen these solely because older Markdown calls them pending:

- the older direct-eval lexical-closure fixes are landed; the two direct-eval issues in track 1
  are different defects;
- the RegExp embedded-NUL and terminal-backslash fixes are landed;
- the prefix/postfix parser fix for forms such as `!c++ && 1` is landed;
- the Broiler.CSS evaluator used by `CSS.supports()` is landed;
- the main `document.currentScript`, `readyState`, `requestIdleCallback`, `sessionStorage`, and
  `structuredClone` surfaces have later fixed evidence, although narrower edge cases above remain;
- Broiler.HTML static-renderer exclusions do not prove that the aggregate Browser/HtmlBridge stack
  lacks every excluded API; and
- Minimal and deliberately narrow Portable profiles are not gaps in the Full profile.

Removed or proprietary surfaces such as WebSQL and `chrome.loadTimes()` are not roadmap work.
Diagnostics that merely hide first-chance exceptions are useful tooling work but are not language
feature gaps and are not tracked here.

## Completion gate

This roadmap is complete when:

1. every confirmed item is fixed or converted into an explicit, reviewed product exclusion;
2. supported Test262 and applicable WPT modes produce trustworthy, reproducible results;
3. no runner shim or shape-only stub is required to claim a supported JavaScript feature;
4. every unsupported global or method has deterministic detection and failure behavior;
5. the retest queue is empty or contains only dated, explicitly deferred investigations; and
6. the component roadmaps, support matrix, known-gap inventory, and compliance dashboard agree.
