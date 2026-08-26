# Broiler.JS gaps — closed

> Part of the [Broiler.JS gaps](broiler-js-gaps-roadmap.md) set:
> **closed** · [open](broiler-js-gaps-open.md) · [in progress](broiler-js-gaps-in-progress.md) · [won't fix](broiler-js-gaps-wont-fix.md).
> Statuses were last reconciled on **2026-08-26**. Every **fixed** entry names where it landed — the
> pinned `Broiler.JS` commit, or the main-repo component for a host/DOM fix — and the regression that
> holds it.

Gaps that are resolved, in two kinds. **Fixed** entries were real defects and each keeps its root
cause, what landed, and the evidence. **Retired** entries were investigated against the current
pointer and did not reproduce; they keep the exact cases tried, because the roadmap's retest rule
requires a non-reproduction to be recorded rather than silently dropped. Both kinds are closed, and
neither should be reopened without new evidence.

## Fixed

### Track 0 — Conformance evidence

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

### Track 1 — Direct eval, parser, scope, and control flow

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
- **A method call whose argument is a method call is silently skipped inside an `async` function.**
  **Root-caused and fixed.**
  `trace.push(items.join('+'))` did not run: no error was raised and the statement after it ran
  normally, and `console.log(list.join(', '))` is the same shape, which is what made a
  small-looking bug worth chasing.
  <br>**Cause.** The receiver and the resolved method of a member call live in two temps taken from
  a per-function pool, and the arguments are compiled *before* those temps are acquired, so a nested
  call in the arguments is handed the very same two back. Ordinary code survives that, because both
  values are on the IL evaluation stack by the time an argument runs. A generator or async body does
  not: `FlattenBlocks` — the generator rewrite's last pass — lifts any block-valued operand's
  statements out as siblings and passes a spilled temp in its place, and a nested member call
  compiles to exactly such a block (`Assign(recv, target); Assign(callee, recv[name]);
  Invoke(callee, …)`). Once hoisted, *its* two assignments run between the outer call's assignments
  and the outer call's invocation, so the outer call invokes the inner callee on the inner receiver.
  `var r = t.push(g.join('+'))` leaving `r` holding the **inner** call's value is the observation
  that named it. The roadmap's own guess — `FlattenBlocks.VisitCall`'s operand hoisting — was half
  right: hoisting is the trigger, but the argument reaches it through `VisitNew` (the `Arguments`
  constructor), which is why neutralizing `VisitCall` alone changed nothing.
  <br>**Fix.** This is the same defect already fixed for `obj.hit(await Promise.resolve(1))`, but
  the suspension was never the cause — the hoist is. That fix guarded on an AST scan of the source
  for `await`/`yield`, which cannot see the far more common plain nested call. The guard now asks
  what the operands *compiled to*: a bare parameter or a constant emits no statements, so nothing
  can be hoisted out of it and the pool stays safe; anything else gets locals on the call's own
  block, which cannot be handed to another call. That direction of approximation costs two locals
  and cannot be wrong, where the old one could and was. Ordinary functions still pay nothing. The
  private-name key temp is pooled the same way and reachable by `this.#a(this.#b())`; it gets the
  same treatment.
  <br>**Evidence.** Eleven regressions in `SuspendedCallArgumentTests`, none containing an `await`
  or a `yield`; ten of the eleven fail before the change and the class is 31/31 after. Engine suites
  with the patch applied: integration 5178/5179, built-ins 2215/2215, compiler 1400/1402, modules
  104/104, core/parser/runtime/module-extensions clean — the three failures are pre-existing and
  unrelated, and reproduce with the change stashed.
  <br>**Live.** Upstream in `Broiler.JS` as *Stop a nested member call clobbering the outer call
  inside a generator body* (`5b5e92f9`) and carried by the pinned pointer, so CI compiles it. It was
  first handed over as a patch file, and a main-repo fallback was never possible — the defect is in
  how the compiler allocates temps for a member call, and nothing at a main-repo layer can intercept
  call compilation.
- `for await…of` **hangs the agent** when the step result is not already settled. **Fixed.** This
  was a deadlock rather than a wrong answer, which is why it never appeared as a failing test:
  `JSIterator` unwrapped the result of `next()` with `promise.Task.GetAwaiter().GetResult()` — a
  blocking wait, on the one thread allowed to run a context's JavaScript. That works only while the
  promise is *already* settled, which is why every shape the engine's suite covered passed: an array,
  an async generator, an iterator returning `Promise.resolve(record)`. The moment `next()` hands back
  the ordinary `something.then(…)`, the job that would settle it can never run — the queue that runs
  it drains on the way out of the execution the thread is stuck inside, which `JSMicrotaskQueue`'s own
  documentation names as the one pattern it cannot support — so the agent hung until the process was
  killed.
  <br>The step is now three pieces with the state machine's own `await` between them:
  `IElementEnumerator.AsyncNextRaw` calls `next()` and hands the result back unexamined, the compiled
  loop awaits it exactly as it awaits anything else, and `AsyncIterationStep` reads `done`/`value` off
  the settled record. Nothing blocks, so the settling job runs at the checkpoint it was queued for.
  Landed in the pinned `Broiler.JS` submodule (`ab5f797`); seven regressions in
  `ForAwaitUnsettledResultTests`, each of which hung rather than failed before.
  <br>**What it unblocked.** This is what `ReadableStream`'s async iteration was held on — see this
  document's [track 5](#track-5--essential-browser-javascript-apis) entry — and it is the fix behind
  track 0's "12 invalid-program IL failures on top-level `for await`" line, which is why that line
  needs re-measuring on the next corpus run rather than carrying forward.
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

Evidence:

- [Active semantic clusters](../Broiler.JS/docs/compliance/known-gaps.md#active-semantic-clusters)
- [Current component failure clusters](../Broiler.JS/docs/roadmap/Component.md#1-close-the-supported-test262-failure-set)
- [Parameter-shadowing record](../Broiler.JS/docs/roadmap/Phase-3.status.md)
- [Strict async/generator record](../Broiler.JS/docs/roadmap/Archive.md), retained by
  [Measurement.md](../Broiler.JS/docs/roadmap/Measurement.md)

### Track 1 — Objects, arrays, symbols, and Proxy-sensitive behavior

- Symbol own keys enumerate by Symbol-creation order rather than property-creation order.
  **Fixed:** it was a property-storage gap, not a sort to delete — symbol properties lived in
  a hash map keyed by the symbol's creation id, which records no insertion order, so
  `getOwnPropertySymbols` sorted by creation id, `Reflect.ownKeys` used raw hash order, and
  `Object.assign` copied in hash order, all disagreeing. Each object now records its symbol
  keys' insertion order (as `PropertySequence` does for string keys — appended on first add,
  dropped on delete, re-added at the end, position kept on update), and every enumeration path
  reads it through `JSObject.SymbolsInInsertionOrder`. Landed in the pinned `Broiler.JS`
  submodule (`f1b78df`); regressions in `Track1LanguageTests`.
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

### Track 2 — RegExp

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
were deleted and the gitlinks point at commits that contain them. The `patches/` backlog is
empty and the directory is gone with it — a submodule fix is now checked with
`git -C <Submodule> log --oneline --grep '<subject>'`, never by patch number.


### Track 3 — Module syntax

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
- `await` as a module-level identifier was accepted in every binding position (`var await`,
  `let`/`const`, a function name, a parameter, a class name, a catch parameter, a destructuring
  target, a label, and every ImportedBinding shape). **Fixed — the parser now has a module goal.**
  This is the one module early error that could not be inferred from the AST the way the
  duplicate-name rules were: `await` is reserved from a module's first token, before any import or
  export has been seen. The goal travels in `JSCompilationOptions.IsModule`, which is deliberate —
  those options *are* the code-cache key, and the goal changes what the text means, so a module and
  a script with identical source must not share a compile entry. The parser reads it through an
  ambient `CoreScript` scope (the mechanism `AllowTopLevelAwait` already uses, since the parser is
  several assemblies from the host and `IJSCompiler.Compile` carries no options), and that scope is
  entered from the *same* resolved options value that goes into the key, so the two cannot drift
  apart; it wraps the compile factory, so a cache hit never pays for it. Blast radius outside module
  code is nil by construction — every new rejection is gated on the goal, which is false for every
  script compile, and the tests assert that scripts still treat `await` as an ordinary name. Inside
  modules the rule is about bindings only: `await` stays legal as a property name, a method name and
  the operator, including top-level await. Landed in `8e745b4`; regressions in
  `ModuleAwaitReservedTests`.

### Track 3 — Module binding semantics

The part-landed half of track 3. Live import bindings, the remaining defect of the same
family, are characterized but not fixed and stay in
[in progress](broiler-js-gaps-in-progress.md#track-3--module-execution-semantics).

- **Import attributes parse but nothing acts on them.** **Decided and implemented.**
  Nothing read `AstImportStatement.Attributes`, the three
  `export … from` forms discarded theirs outright, and the compiler's call to the loader passed only
  the specifier. So `with { type: 'json' }` — the portable form, and the only one a browser accepts
  on a JSON module — was accepted and ignored, and so was `with { flavour: 'nonsense' }`.
  <br>**Where each failure is raised was measured from Chromium, and the split is principled.** On a
  static declaration the keys are literals, so an unknown key and a duplicate key are early
  **SyntaxError**s, decided in the parser. Whether the `type` *value* names a module type, and
  whether the module it resolves to is of that type, depends on the module, so both are load-time
  **TypeError**s from the host. A dynamic `import()`'s keys are a runtime value, so it reports both
  as TypeErrors — which is what Chromium does. Every message is Chromium's own.
  <br>Attributes reach the host in two shapes through one validation path: a dynamic import's
  runtime options object (validated in full — options an object, `with` an object, every value a
  string), and a static clause as a flat array of alternating key/value strings, flat because every
  part of it is a literal the grammar already fixed and in source order so the *first* offending key
  is the one reported. `export … from` carries its clause through `AstExportStatement` to the same
  place, so all three forms are enforced like the import they perform.
  <br>**The type/module match is the one rule this host cannot implement the way the web does.** A
  browser checks the assertion against the response MIME type; there are none here, so it checks the
  resolved module key — the same fact by the only means available, and the one that already decided
  the module would be parsed as JSON, so the check and the parse cannot disagree. `css` is told apart
  from a typo: a real module type this engine does not implement, reported as such, so a page can
  distinguish "not implemented here" from "not a thing".
  <br>**Deliberate divergence, pinned by a test and still open as a decision:** a `.json` module
  imported with *no* attribute loads. See
  [open](broiler-js-gaps-open.md#needs-a-product-decision).
  <br>**Evidence.** 33 regressions in `ImportAttributeEnforcementTests`, 23 of which fail before the
  change; each error-name assertion goes through a dynamic import so the JS-visible *name*, not just
  the message, is pinned. `ModuleAttributeClauseTests` keeps every grammar case it pinned, retargeted
  — it was written against `with { type: 'javascript' }`, which is not a module type any platform
  defines and which enforcement now correctly rejects. Modules 160/160, parser 198/198, integration
  5178/5179, compiler 1400/1402, built-ins 2215/2215.
  <br>**Live.** Upstream in `Broiler.JS` as *Enforce import attributes instead of parsing and
  dropping them* (`39beed96`) and carried by the pinned pointer. It extends a signature the
  JSON-module fix introduces, so it lands after it.
- **`import.meta` is a SyntaxError.** **Decided and implemented.**
  "import.meta not supported" came from the compiler's meta-property path,
  which handled only `new.target`; deterministic rather than a crash, so it was carried as a
  capability decision. **Decided: implement it, with `url` on it, and leave `resolve` out.**
  <br>It compiles to a read of `meta` off the module record the body already receives as its
  `module` parameter, so the object's identity, its lazy creation and everything on it belong to the
  module host rather than to the compiler — which is what keeps `import.meta === import.meta` true
  and lets a host with its own key form report its own URL without the compiler knowing what a
  module key is. `JSModule.Meta` creates it once and then returns the same object (ES2025 §16.2.1.9;
  a module is entitled to hang state off it), with a **null prototype**, carrying `url` and nothing
  else. The URL comes from a new `JSModuleContext.GetModuleUrl` virtual: an absolute URI verbatim, a
  filesystem path as a `file://` URL. Returning null is meaningful — the object then carries no
  `url` rather than an invented one, so a key that cannot be a URL reads `undefined`, which a script
  can detect.
  <br>**`resolve` is out, and the reason is the resolver rather than the code.**
  `JSModuleContext.Resolve` is existence-based, while `import.meta.resolve` resolves to a URL
  whether or not anything is at it; built on today's resolver it would throw where a browser
  answers — a wrong answer to a resolution question rather than a missing one, and a page can
  feature-detect the absence but not the wrongness. The remainder is stated in
  [open](broiler-js-gaps-open.md#needs-a-product-decision).
  <br>Outside module code it stays an early SyntaxError (§13.3.12), which is what a
  `try { eval('import.meta') }` feature-detect expects; inside module code compiled with no module
  record it is a deterministic ReferenceError rather than a silent `undefined`.
  <br>**Evidence.** Eight regressions in `ImportMetaTests`; every expectation except `resolve` is
  Chromium's measured answer for a module in a page, from the same probe — identity stable,
  prototype `null`, `Object.keys` `['url']`, and a transitively imported module reporting its own
  URL rather than the entry point's.
  <br>**Live.** Upstream in `Broiler.JS` as *Implement import.meta, with url on it and resolve
  deliberately absent* (`33bb2b81`) and carried by the pinned pointer.
- **A JSON module's default import is `undefined`.** **Decided and fixed.**
  This was listed as needing a product decision, and it
  did: the two specifications disagree about what a JSON file exports and one object cannot satisfy
  both, which is why the obvious mechanical fixes all fail. ES2025 gives a JSON module exactly one
  export, `default`, holding the parsed value, and no named exports; CommonJS
  `require('./x.json')` hands back the parsed value itself.
  <br>**The decision: the module stores the ES namespace, and the CommonJS view unwraps it.** The
  wrapper becomes `module.exports = { default: (<json>) }`, and `LoadModuleAsync` takes an
  `esModule` flag — defaulting to the ES view, with the two `require` call sites passing `false` —
  that changes nothing except the shape a JSON module is presented in. Storing the namespace rather
  than decorating the value is what makes arrays, numbers, strings, booleans and `null` work: none
  of them can carry a `default` property. `null` was the sharpest case — `JSModule`'s exports setter
  refuses null, so a file whose whole content is `null` used to *throw on load* rather than import
  as `null`.
  <br>**Deliberate deviation, pinned by a test:** `import { a } from './x.json'` used to read `a`
  off the parsed object and is now `undefined`. Per spec it is a link error, which needs
  whole-module link analysis this engine does not do, so the nearer of the two available answers is
  taken; browsers and Node both reject the form, so nothing portable relied on it.
  <br>**Evidence.** Fourteen regressions in `JsonModuleTests` — the default import over six JSON
  shapes, the namespace holding only `default`, a dynamic import resolving to the same namespace,
  `require` over three shapes, both views of one file agreeing on the value, the named-import
  deviation, and a JavaScript module pinned as unaffected. Eleven of the fourteen fail before the
  change. Modules 118/118, module-extensions 5/5, built-ins 2215/2215, integration 5167/5168,
  compiler 1400/1402, with the three failures pre-existing and unrelated.
  <br>**Live.** Upstream in `Broiler.JS` as *Give a JSON module the ES namespace ESM wants and
  require the value CommonJS wants* (`c1c295e2`) and carried by the pinned pointer.
  `BridgeModuleContext` overrides only the resolution/read seams, so it is unaffected by the change.

The two `Broiler.JS` patches carrying these three fixes were written as submodule patches because
the push to the submodule remote returned 403 (it is outside this session's GitHub scope). They have
since been applied upstream and the gitlink bumped: `12839186` *Give a module's top-level lexicals their own environment* and
`8b74d6c3` *Make an imported binding immutable* are both ancestors of the pinned pointer, and the
`patches/` copies are deleted. Check either with
`git -C Broiler.JS log --oneline --grep '<subject>'` rather than by patch number.

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
- **An imported binding was mutable.** An `ImportedBinding` is immutable (ES2024 16.2.1.5 creates it
  as an immutable binding), but the engine seeded each import into an ordinary mutable local, so
  `import { x } from 'm'; x = 2;` quietly overwrote the local snapshot and ran on. **Fixed** by
  sealing every import binding — named, default, namespace, and renamed — read-only right after it is
  seeded, so a later assignment throws in the strict module code, the same runtime read-only
  TypeError a reassigned `const` gives. The spec makes assignment to an import an *early* SyntaxError;
  this engine cannot raise that phase without whole-module scope analysis across its deferred function
  bodies (an assignment inside a not-yet-compiled function body is not seen at module-compile time),
  so it matches its own `const` treatment — a runtime read-only write — rather than leaving the write
  to succeed. The submodule commit is *Make an imported binding immutable*; regressions in
  `ModuleImportImmutabilityTests`.

### Track 5 — Essential browser JavaScript APIs

- **Async iteration over a `ReadableStream` is on.** `ReadableStream.prototype.values` and its
  `@@asyncIterator` were written, correct and verified when the stream landed, and were deliberately
  left commented out: `for await` deadlocked the agent on an iterator whose `next()` returned a
  promise that was not already settled, and `reader.read().then(…)` — what an iterator over a stream
  returns — is exactly that shape. Installing them then would have turned the ordinary
  `for await (const chunk of response.body)` from a `TypeError` a page's script survives into a
  capture that never settles, which is strictly worse than the `TypeError`. The engine fix is upstream
  and the pinned `Broiler.JS` pointer carries it (track 1, *`for await…of` hangs the agent when the
  step result is not already settled*), so both statements are installed.
  <br>`next()` releases the reader's lock on `done` rather than on the following call, so a loop that
  runs to completion leaves the stream unlocked; `return()` — which the engine calls when a loop is
  left by `break`, `return` or `throw` — cancels the source unless `preventCancel` was asked for, then
  releases. Seven regressions in `ReadableStreamTests` replace the one that pinned the absence
  (`Async_Iteration_Is_Absent_Until_The_Engine_Can_Drive_It`, now deleted): chunk order and lock
  release over a page stream, a blob stream and a response body; early exit cancelling the source with
  the loop's completion value as the reason; `preventCancel` releasing without cancelling; an errored
  stream throwing into the loop with the queued chunk discarded; a locked stream rejecting with a
  `TypeError`; and `@@asyncIterator` being the same function object as `values`, on the prototype.
  Every expectation is Chromium's measured answer to the same probe — the two agree on all seven.
  `pipeTo`/`pipeThrough` and BYOB readers stay absent and detectably so; they are their own
  capabilities, not pieces of this one.
- **Three of `navigator`'s object-valued surfaces are decided and implemented, and three are decided
  and absent.** Each is a whole API rather than a value, and the test the audit line named is whether
  a present object answers a page's `'x' in navigator` detection *more* misleadingly than absence
  does — the same test that kept `speechSynthesis` and `navigator.bluetooth` out. Six decisions, one
  reason each.
  <br>**`navigator.storage` (`StorageManager`) — implemented.** It reports quota-managed storage:
  IndexedDB, the Cache API, the origin private file system. Broiler implements none of them, so the
  honest estimate is `{usage: 0, quota: 0}` and the honest persistence answer is `false`. That is the
  same pair the already-present `navigator.webkitTemporaryStorage` reports for the same question
  through the deprecated interface — the two disagreed only by one of them being absent.
  `getDirectory()` is deliberately *not* on it: the origin private file system's feature-detect is
  exactly `'getDirectory' in navigator.storage`.
  <br>**`navigator.permissions` — implemented, with one deliberate divergence.** Broiler grants no
  permission-gated capability and has no surface to prompt on, so every query answers `"denied"` — a
  real, specified state, and the one `Notification.permission` already reports for the single
  capability that had an answer at all. Chromium answers `"prompt"`; that state promises a dialog
  this engine cannot show. A name outside the `PermissionName` enum rejects with a `TypeError`
  carrying Chromium's message, because the enum is validated before the permission is looked at, so a
  typo is reported as a typo rather than as a denial.
  <br>**`navigator.userAgentData` (`NavigatorUAData`) — implemented.** Identity is the one thing the
  bridge already reports carefully, and every member here is derived from the single
  `BroilerUserAgent.Value` string, so the structured form and the string cannot disagree — which is
  the whole argument for exposing it. `brands` carries the major version only (that is what makes it
  low-entropy) and `getHighEntropyValues` answers exactly the hints it is asked for, from the same
  string. No GREASE brand is invented: the anti-ossification second entry a browser adds is a
  browser-market argument rather than a correctness one, and it would name a product that does not
  exist.
  <br>**`navigator.connection` — absent, deliberately.** `NetworkInformation` claims the user agent
  can report the connection's quality: `effectiveType`, `rtt`, `downlink`. Broiler measures none of
  it, and the interface has no "not known" state, so any value would be an invention rather than a
  negative answer.
  <br>**`navigator.mediaDevices` and `navigator.mediaCapabilities` — absent, deferred.** Both are
  media surfaces, and their capability decisions belong with the rest of media in
  [open](broiler-js-gaps-open.md#media-communications-devices-and-security) rather than being taken
  here on their own.
  <br>The members live on the interface prototypes and each surface is a singleton, so an instance
  carries no own properties. Over a 22-case corpus run, Broiler and Chromium agree on every case but
  the permission state, which is the divergence above. The absences are pinned by a regression, so
  they stay decisions rather than drifting back into omissions.
  <br>Main-repo fix (the new `Features/NavigatorSurfacesBinding.cs` plus
  `DomBridge/Registration/Window.cs`); regressions in `NavigatorSurfacesTests`.

- **A navigation entry's `duration` was a hardcoded `0`.** Navigation Timing §4 defines it as
  `loadEventEnd - startTime`, and a navigation entry's `startTime` is `0` by definition, so it *is*
  `loadEventEnd`. `entry.duration` is the shortest way a page writes "how long did this take", so a
  `0` there is a plausible number rather than an absent one and nothing distinguishes the two —
  which is what made this worse than the network phases it sat beside, where at least the whole
  family read `0` together.
  <br>The constant was right for exactly one case, which is what kept it looking correct: read
  *before* the load event ends, `0` is the specified value for a moment not yet reached. It was
  pinned by an assertion whose own comment said duration "is 0 *until* the load event ends" — and the
  fixture read it from a `load` listener, where the mark has genuinely not been stamped. **Fixed** as
  a live accessor over `loadEventEnd`, so the before-load `0` is unchanged and a read afterwards
  reports the figure the entry already measures beside it. Main-repo `Broiler.HtmlBridge.Dom` fix
  (`Features/NavigationTimingBinding.cs`); regressions `The_Duration_Is_LoadEventEnd` — which reads
  from a task the load handler schedules, since `loadEventEnd` is stamped when the dispatch returns,
  the same reason analytics read this from a timeout rather than from `onload` — and
  `The_Duration_Is_Zero_Before_The_Load_Event`, which keeps the case the constant got right.
  Found while wiring the network phases below and deliberately left out of that change, so each
  landed as its own.
- **The navigation entry's network phases were not measured.** `fetchStart`, `domainLookup*`,
  `connect*`, `secureConnectionStart`, `request*`, `response*` and the
  `transferSize`/`encodedBodySize`/`decodedBodySize` trio existed and reported `0` — which in
  Navigation Timing means "not observed", not "instantaneous". The arithmetic built on them yielded a
  number rather than `NaN`, but not a measurement, and no feature test could tell the two apart.
  <br>**The time origin is what the fix turns on, not the instrumentation.** A mark is milliseconds
  since the document's time origin, and the origin is the navigation's start (HR-Time §5). The bridge
  stamped its origin when it built the `performance` object — already *after* the fetch — so every
  real network mark would have been negative and clamped to the specification's floor of `0`. Which
  is to say the zeros were not laziness: they were the only expressible answer under that origin.
  **Fixed** by having the host take the origin before the fetch begins and hand it across with the
  measurements, so `performance.now()`, the lifecycle marks and the network phases are all measured
  from one instant, as a browser measures them.
  <br>The measuring is the host's because only the host can see it — the document is fetched before
  the bridge exists. `CaptureService` marks `fetchStart` around its own fetch and reads the phases
  only the connection can show from a `SocketsHttpHandler.ConnectCallback` (which performs the DNS
  lookup and the socket connect itself, so their boundaries become observable) plus a
  `PlaintextStreamFilter` for the instant the connection is usable after a TLS handshake. The request
  goes out with `HttpCompletionOption.ResponseHeadersRead`, so `responseStart` — the headers
  arriving — is a separate instant from `responseEnd`, which buffering the whole body first collapses
  into one. `requestStart` is taken in the connection handler rather than before `SendAsync`: the
  connection is opened *inside* that call, so a mark taken before it precedes `connectEnd`, which the
  specification's ordering forbids.
  <br>A phase a fetch did not perform reports the previous phase rather than `0`, which is what the
  specification asks for: a `file:` document looks up no host and opens no connection, so its lookup
  and connect marks collapse onto `fetchStart`. `secureConnectionStart` is the documented exception
  and stays `0` when no handshake happened. `transferSize` is the payload plus the response header
  fields, and the header bytes are reconstructed from the response rather than counted — the bytes
  that carried them are gone by then — which is exact for the fields and approximate for the status
  line. A host that fetched nothing supplies no timing at all, and the marks then keep reporting `0`:
  HTML handed to the bridge as a string, the conformance runner, and almost every test take that
  path, so their behaviour is unchanged.
  <br>Main-repo fix: the `DocumentFetchTiming` type in `Broiler.HtmlBridge.Core` (the parent owns it,
  so the measuring host and the reporting bridge share one contract), `DomBridge.DocumentFetchTiming`
  and `Features/NavigationTimingBinding` in `Broiler.HtmlBridge.Dom`, and the instrumentation in
  `Broiler.Cli`'s `CaptureService`; landed directly rather than as a submodule patch. Regressions in
  `NavigationTimingNetworkPhasesTests`, which pin the supplied-timing, no-timing and collapsed-phase
  cases, the shared timeline, and one end-to-end capture against a local origin that measures its own
  fetch. The entry's `duration`, found here and deliberately left out of this change, is the entry
  above.
- `performance.now()` returned `Date.now() - timeOrigin`: whole-millisecond wall-clock arithmetic. It
  had no sub-millisecond resolution, and — being wall time — could run **backwards** when the system
  clock was stepped (NTP, a manual change), which HR-Time §3 forbids (the value "MUST be monotonically
  increasing and not subject to system clock adjustments"). **Fixed:** the `performance` object now
  captures a `Stopwatch` timestamp at the same instant as the wall-clock `timeOrigin`, and
  `performance.now()` measures monotonic elapsed time from it as fractional milliseconds. `timeOrigin`
  stays the wall-clock estimate of the origin (HR-Time §5), so `timeOrigin + now()` still tracks
  `Date.now()`, while `now()` itself is monotonic and sub-millisecond. Privacy coarsening of the
  resolution is a separate decision and was not added. This is a main-repo `Broiler.HtmlBridge.Dom`
  fix (`WindowDocumentMiscBinding.PerformanceNow` / `Window.RegisterPerformanceObject`), landed
  directly rather than as a submodule patch; regression `Performance_Now_Is_Monotonic_And_SubMillisecond`
  in `GoogleSearchPolyfillTests`. The Navigation Timing marks that measure against this same origin
  are the entry above.
- Seven script-visible document surfaces were absent — `document.charset`, `referrer`, `domain`,
  `lastModified`, `activeElement`, `hasFocus()`, and the `onvisibilitychange` handler slot — the
  audit line that also named `window.trustedTypes`. Each read `undefined` (or, for `hasFocus`, was
  missing outright), which is not the same as answering "none": a page comparing
  `document.domain === location.hostname`, stringifying `document.referrer` into a beacon, or calling
  `new Date(document.lastModified)` saw a third state it had no branch for, and
  `document.activeElement.tagName` threw. **Fixed**, each with the value the specification gives for a
  directly-navigated, permanently-visible capture rather than a placeholder: `charset` is the third
  historical alias of `characterSet` (DOM §4.5) and returns the same `UTF-8`; `referrer` is the empty
  string, which is what HTML defines for a document with no referrer; `domain` is the origin's
  effective domain, i.e. the page URL's host, and the empty string for the opaque origin of a
  host-less URL such as `data:`; `lastModified` is the current local time in the specified
  `MM/DD/YYYY hh:mm:ss` shape, which is HTML's own stated fallback when the source's modification date
  is unknown; `activeElement` is the `body` element, because HTML's algorithm ends "if candidate is
  null, set candidate to the body element" and a capture focuses nothing; and `hasFocus()` is `true`
  for the same reason `visibilityState` is `"visible"` — one document in one viewport, never
  backgrounded and never defocused. `onvisibilitychange` is a handler slot defaulting to `null` whose
  event never fires, which is the accurate outcome rather than a missing implementation (a
  permanently-visible document's visibility never *changes*); it has to exist because
  `'onvisibilitychange' in document` is the feature test that decides whether a page uses the Page
  Visibility API at all. `window.trustedTypes` was deliberately **not** added and stays in
  [open](broiler-js-gaps-open.md#window-document-navigator-url-and-timing-semantics) as a capability
  decision: it is an enforcement API, and a shape-only stub would claim a policy mechanism that does
  not exist. Main-repo `Broiler.HtmlBridge.Dom` fix
  (`Registration/Document.cs`, `Features/WindowDocumentMiscBinding.cs`), landed directly rather than as
  a submodule patch; regressions in `DocumentSurfaceTests`.
- `fetch()` returned a self-returning thenable rather than a conforming Promise — track 5's action 1.
  There were two such hand-rolled objects: the one `fetch()` returned, and the one behind every body
  method (`.text()`, `.json()`, `.arrayBuffer()`, `.blob()`, `.formData()`, and a stream reader's
  `read()`). Both carried a `then` that invoked the callback and returned **themselves**, and that is
  the defect worth naming: `.then(a).then(b)` ran `b` against the ORIGINAL value instead of `a`'s
  result, so the ordinary `fetch(u).then(r => r.json()).then(useData)` shape handed the second
  callback the Response rather than the parsed body — a **silently wrong value, not an error**.
  Alongside it: `.then`'s second (onRejected) argument was ignored entirely; `.finally` did not exist,
  so calling it was a TypeError; a callback that threw was caught and logged rather than rejecting the
  derived promise, so an error inside a handler vanished; the body thenable had no rejection path at
  all, so a resolver that threw (`.json()` over a malformed body) threw synchronously out of `.then`
  instead of rejecting; and neither object was `instanceof Promise`, which feature-detecting code
  checks. **Fixed:** both are now real `JSPromise`s, settled from the outcome already in hand. The
  engine's microtask queue is pumped in a capture — a plain `Promise.resolve().then(...)` callback
  runs, which was verified before the change — so settling through the real machinery still delivers
  the callbacks.
  <br>Two existing tests asserted the **old** behavior and were updated with their intent preserved,
  which is worth flagging rather than burying: `Fetch_Response_Json_InvalidJson_Throws_Clear_Error`
  asserted the synchronous throw and now asserts the rejection (still checking the same clear message
  and that `bodyUsed` is set) — renamed to `…_Rejects_With_A_Clear_Error`; and
  `XHR_ReadyStateChange_Fires_For_Loading_State_Before_Done` read `readyState` history synchronously
  after `send()`, which only worked because the polyfill's `response.text()` resolved synchronously.
  XHR still reaches readyState 4 and delivers status and body — verified end to end — it now does so
  on a microtask, the same asynchrony a browser has for an async XHR, so the test observes after the
  queue drains and still asserts LOADING arrives, in order, before DONE. Main-repo
  `Broiler.HtmlBridge.Dom` fix (`Features/FetchBinding.cs`, `Features/FetchBinding.Callbacks.cs`),
  landed directly rather than as a submodule patch; regressions in `FetchPromiseConformanceTests`.
- The `PerformanceNavigationTiming` entry carried **no timing attributes at all**, and absent is the
  one thing they may not be: these are read inside subtraction far more often than alone, so the
  ubiquitous RUM idioms — `responseEnd - requestStart`, `domComplete - domInteractive` — produced
  **NaN** rather than a duration, silently. **Fixed**, with the attribute set split by what this
  engine can honestly observe.
  <br>The **document-lifecycle marks are measured**: `domInteractive`,
  `domContentLoadedEventStart`/`End`, `domComplete` and `loadEventStart`/`End` are stamped by the
  bridge's own load sequence at the moments it genuinely reaches — `readyState` becoming
  "interactive", the `DOMContentLoaded` dispatch, `readyState` becoming "complete", the `load`
  dispatch — using the same monotonic clock and time origin `performance.now()` measures from, so a
  mark and a `now()` reading are two points on one timeline (a regression asserts
  `domComplete <= performance.now()`, and another asserts the marks advance in order). An ordering
  wrinkle had to be solved for this: the entry is built while `performance` is registered, which is
  *before* any of these happen, so the entry reads them through a small mutable holder
  (`NavigationTimingState`) rather than holding values fixed at construction.
  <br>`redirectStart`/`End`, `unloadEventStart`/`End` and `workerStart` are `0` because the phase
  genuinely did not occur — nothing redirected, there is no previous document to unload, no service
  worker intercepted — which is the value the specification gives each.
  <br>The **network phases are not measured and the zeros say so**. `fetchStart`, `domainLookup*`,
  `connect*`, `request*`, `response*` and the body-size trio report `0`, the specification's "no
  information" value, because nothing at this layer observed them: the document is fetched by the
  capture host before the bridge exists, and the time origin is stamped after that fetch, so any real
  value would be negative and `0` is the floor. They are present so the arithmetic yields a number
  instead of NaN — a duration of `0` across an unobserved phase, **not** a claim that it was
  instantaneous. Measuring them properly is a cross-layer change and is recorded in
  [open](broiler-js-gaps-open.md#window-document-navigator-url-and-timing-semantics). Main-repo
  `Broiler.HtmlBridge.Dom` fix (`Features/NavigationTimingBinding.cs`, the new
  `Features/NavigationTimingState.cs`, and the stamp points in `DomBridge.WindowLoad.cs`), landed
  directly rather than as a submodule patch; regressions in `NavigationTimingMarksTests`.
- `navigator`'s identity and hardware surface was absent — the privacy inventory's #1748, ten probes.
  `appCodeName`, `appName`, `appVersion`, `product`, `productSub`, `webdriver`, `deviceMemory`,
  `hardwareConcurrency` and `maxTouchPoints` all read `undefined`, which is the one answer none of
  them may have: five are constants HTML §8.9 *mandates* for every user agent, and the rest are read
  inside arithmetic and comparisons where an absent value propagates silently rather than announcing
  itself — `navigator.appVersion.indexOf(…)`, still the shape of a great deal of legacy sniffing,
  threw outright. **Fixed**, with each value chosen from what this engine actually is:
  the five legacy constants are the specification's fixed strings (`"Mozilla"`, `"Netscape"`,
  `"Gecko"`, `"20030107"`), which are *not* vendor identity claims — §8.9 pins them for every browser
  regardless of engine precisely so that sniffing them tells a page nothing, and returning anything
  else would be the deviation; `appVersion` is **derived** from the one user-agent string rather than
  written out a second time, so it cannot drift from `navigator.userAgent` (a regression asserts
  `'Mozilla/' + appVersion === userAgent`); `webdriver` is `true`, the honest answer rather than the
  flattering one, because the attribute reports whether the agent is driven by automation and a
  capture engine is exactly that; `hardwareConcurrency` is the machine's real logical-processor
  count and `deviceMemory` its real memory coarsened as the Device Memory specification requires
  (a power of two, clamped to 0.25–8), so both are measured rather than asserted; and
  `maxTouchPoints` is `0` because a capture has no touch input.
  <br>`vendor` was deliberately **left unchanged** at `""`. §8.9 permits exactly `""`,
  `"Apple Computer, Inc."` or `"Google Inc."`; Broiler's user agent does not claim to be Chrome, so
  `""` is both conforming and truthful, and changing it to match Chromium's answer would be an
  identity claim rather than a fix. The object-valued surfaces beside these (`connection`,
  `permissions`, `storage`, `mediaDevices`, `mediaCapabilities`, `userAgentData`) are whole APIs, not
  values, and stay in [open](broiler-js-gaps-open.md#window-document-navigator-url-and-timing-semantics)
  pending the same present-but-empty-object judgement that kept `speechSynthesis` out.
  This also picked up `window.offscreenBuffering`, the one member of the window/screen block below
  that the geometry fix missed. Main-repo `Broiler.HtmlBridge.Dom` fix (the new
  `Features/NavigatorIdentityBinding.cs` and its registration), landed directly rather than as a
  submodule patch; regressions in `NavigatorIdentityTests`.
- Window and screen geometry plus the `BarProp` objects were absent: `window.screenX`/`screenY` (and
  the `screenLeft`/`screenTop` spellings), `window.devicePixelRatio`, `screen.availLeft`/`availTop`,
  and all six of `locationbar`, `menubar`, `personalbar`, `scrollbars`, `statusbar`, `toolbar`.
  `innerWidth`/`outerWidth`/`screen.width` already answered, so this was the remainder of the pairs
  around them. **Fixed**, each with the value that follows from what a capture is rather than a
  placeholder: the viewport *is* the screen (`screen.width` is the viewport's own width), so the
  window sits at the screen origin and all four position members are `0`, and nothing — no dock, no
  taskbar — is reserved out of the available area, so `availLeft`/`availTop` are `0` beside the
  avail sizes that already equalled the full screen. `devicePixelRatio` is `1` because the renderer
  has no device-scale or backing-store-scale concept at all, so a CSS pixel is a rendered pixel;
  page zoom is a separate axis and is already reported by `visualViewport.scale`. Every `BarProp`
  reports `visible: false` because no browser user interface is painted and no scrollbar is painted
  either — which the already-published `outerWidth == innerWidth` asserts independently, so the two
  agree rather than contradict.
  <br>The absence mattered most inside arithmetic, which is how these members are usually read: an
  absent member is `undefined`, so the centre-on-parent popup idiom
  (`screenX + (outerWidth - w) / 2`), the canvas backing-store idiom
  (`width * devicePixelRatio`) and `availLeft + availWidth` each produced **NaN** rather than a
  coordinate — silently, with no error to trace. A `BarProp` was worse than a wrong boolean: the
  objects are containers, so the documented `window.locationbar.visible` threw "Cannot get property
  visible of undefined", aborting the rest of the calling function. Main-repo
  `Broiler.HtmlBridge.Dom` fix (`DomBridge/Registration/Window.cs` and the new
  `Features/WindowBarPropBinding.cs`), landed directly rather than as a submodule patch; regressions
  in `WindowScreenGeometryTests`.

### Track 6 — DOM, CSSOM, SVG, and script-visible document behavior

- **A character-data node's interface members were own properties of the wrapper, not on the
  interface prototypes.** **Fixed, and live** — entirely in the main repo
  (`Broiler.HtmlBridge.Dom`), so no patch. This is the first interface of track 6 action 1 to move;
  the rest is in [open](broiler-js-gaps-open.md#dom-interface-and-collection-model).
  <br>**Cause.** The bindings install every member as an own property of each wrapper, so an
  interface prototype carried nothing of its own: `Text.prototype.splitText` was `undefined` and a
  text node listed 57 own properties where a browser gives it none. The prototype *chain* was already
  real (`Text → CharacterData → Node → EventTarget → Object`) and the interface objects already
  existed — what had not happened is the engine putting its members on them.
  <br>**Fix, and the mechanism the rest of the item needs.** A member on a prototype has no node
  captured in a closure, so it finds one from its receiver. That needed a wrapper→node lookup:
  `JsObjectRegistry` had one, but it was an O(n) scan over every wrapper the document had minted —
  fine for the handful of call sites that had it, and not fine per DOM operation, so it is now a
  reverse map and constant-time. On top of that, the members a text or comment node exposes are
  installed on `Node.prototype`, `CharacterData.prototype` and `Text.prototype` — the split Web IDL
  specifies, so a page walking a prototype's own property names reads the shape a browser has, and
  `splitText` is `Text`'s alone rather than something a `Comment` inherits. The 18 node constants
  were already on `Node.prototype`; each instance carried a duplicate set, which is simply gone.
  <br>**It closed an identity bug of its own.** `splitText` dropped the node's wrapper afterwards —
  "invalidate the cached JSObject so length/data properties reflect the update" — from when the
  members captured state. DOM §4.11 splits a text node *in place*, so `target.firstChild === t` must
  hold after `t.splitText(n)`; measured in Chromium it does, and here the next wrapper minted for the
  node was a different object. With the members reading the live node through the receiver there is
  nothing to invalidate, so the invalidation and its now-dead host member are gone.
  <br>**What it leaves.** A text or comment node carries three own properties instead of 57 — the
  `EventTarget` trio, which cannot simply be inherited and is stated in
  [open](broiler-js-gaps-open.md#dom-interface-and-collection-model) with the reason. Elements and
  documents are untouched, so the members installed on `Node.prototype` are shadowed for them and
  nothing about them changes; a wrapper minted before the realm carries the interfaces still installs
  its own members, which is the old shape rather than a broken one.
  <br>**Evidence.** 16 regressions in `CharacterDataInterfacePrototypeTests` — the members answering
  from the prototype, the instance carrying none of the interface, `splitText` not reaching a
  `Comment`, the constants inherited rather than copied, the operations still behaving, the split
  keeping its identity, a foreign receiver being a `TypeError`, and a page extending
  `CharacterData.prototype` reaching instances. Whole-suite diffs against a same-container baseline
  show no regression: `Broiler.Cli.Tests` 39 distinct failures → 39, `Broiler.Wpt.Tests` 52 → 51.
  Two tests differed and neither is this change — an image-prefetch test and a thread-budget one,
  both verified to pass in isolation, and the 20s render guard the previous change had seen flake
  passes here.

- **`getComputedStyle` did not apply the user-agent stylesheet's `display`.** **Fixed, and live** —
  entirely in the main repo (`Broiler.HtmlBridge.Dom`), so no patch.
  <br>**Cause.** Every element whose display comes from the UA sheet rather than an author rule
  reported `inline`, the CSS initial value — a plain `<div>` as much as a `<table>`, a `<script>` or
  a `<head>`. The bridge has both the table and the resolution (`CssUserAgentDefaults.DisplayValues`
  and `ApplyUserAgentDisplayDefaults`) and applies them to the *sparse* projection its ~90 internal
  layout/anchor/hit-test consumers read, but the JS binding's map is built by the engine's
  `GetComputedStyle`, which backfills initial values. So the seed could not simply be called on it:
  `ApplyUserAgentDisplayDefaults` fills an *absent* `display`, and after the backfill the key is
  always present — holding `inline`. Nothing the UA sheet said about `display` reached script.
  <br>**Not a rendering defect.** The renderer reads the box tree and the internal consumers read
  the sparse map, both of which carried the right value; only the CSSOM answer was wrong. That is
  why it survived so long, and why it was mis-attributed once — the `foreignObject` record below
  blamed the missing box for the `inline` its content reported, when a plain `<div>` in the body
  answered `inline` too.
  <br>**Fix.** `getComputedStyle`'s map takes `display` from `GetComputedProps` — the same memoised
  sparse projection, with the explicit-`inherit` fold and the UA seed already applied — rather than
  recomputing it. So the two paths cannot answer differently about what an element's display is, and
  the seed stays non-clobbering: an author rule, an inline style and `[hidden]`'s loss to an author
  rule all behave exactly as before.
  <br>**Evidence.** 33 regressions in `UserAgentDisplayComputedStyleTests`, every expectation
  Chromium's measured answer to the same markup from one probe run against both; 27 of them fail
  before the change and all 33 pass after. A 32-tag probe went from every tag answering `inline` to
  30 of 32 matching Chromium. Whole-suite diffs against a same-container baseline show no regression:
  `Broiler.Cli.Tests` 38 distinct failures → 38, `Broiler.Wpt.Tests` 53 → 52. Three tests differed
  between those runs and none is this change: `ScriptCompileAheadOverlapTests`'
  thread-budget test (whose own record says it "fails on a loaded CI box for reasons that have
  nothing to do with the code"), the 20s `ScrollWriteGeometryTimeoutTests` render guard, and two
  `RunnerModuleScriptTests` — each verified to pass in isolation with the change in place, the render
  guard in 8–9s against its 20s ceiling.
  <br>**What it did not close.** Two tags where Broiler still differs from Chromium, both gaps in the
  shared tag→display table rather than in the path that now reads it, and both predating this change
  — they were invisible while every element answered `inline`. They are stated in
  [open](broiler-js-gaps-open.md#cssom-fonts-svg-and-js-visible-layout-algorithms) and pinned by
  `TheTwoTagsWhereTheTableDivergesFromChromiumArePinned`.

- **`foreignObject` content was not laid out at all.** **Fixed, and live** — entirely in the main
  repo (`Broiler.Layout`), so no patch and no submodule pointer to bump.
  <br>**Cause.** SVG internals are not CSS-visible here — the subtree is serialised back to markup
  and drawn by `SvgRenderer` — so `DomParser.CascadeApplyStyles` set every child box of an outermost
  `<svg>` to `display: none`. That is right for shapes and wrong for exactly one element: SVG 2 §12.1
  makes `<foreignObject>` the one place an SVG subtree re-enters CSS layout. Hidden with the shapes,
  its content had no box at all — a `<div>` inside one reported `0,0,0,0` and an
  `offsetWidth`/`offsetHeight` of `0`, so `elementFromPoint` over the child answered the
  `<foreignObject>`. The element *itself* always had a rect, resolved from its own geometry
  attributes like any other shape, which is why the gap was in the subtree rather than in the
  element.
  <br>**Fix.** `Broiler.Layout.Engine.SvgForeignObjectBoxes` lifts each `<foreignObject>` back out
  after the cascade and before layout: it becomes an absolutely positioned block at its user-space
  `x`/`y`, sized from its `width`/`height`, inside the viewport box — which the pass makes a
  containing block by giving it `position: relative`, and only when the document actually holds one,
  so a document without one lays out exactly as before. The HTML children keep the styles the cascade
  already gave them (the cascade descends through hidden boxes; only layout skips them) and lay out
  by the ordinary rules, with no special case below that point. One reached through a `<g>` chain is
  re-parented onto the viewport box rather than having the chain un-hidden, so the hiding of every
  other box stays byte-identical. `SvgStructure` gains `foreignObject` as a non-painting container so
  nothing inside one is also drawn by the renderer's shape passes.
  <br>**Additive, so it needed no submodule push.** The natural call site is `DomParser`, which is in
  `Broiler.HTML` and outside this session's GitHub scope — the push returns 403. Rather than ship the
  fix as a patch that CI would not carry, the pass leaves the parser's hiding exactly as it is and
  only un-hides the one box, which makes it a box fix-up rather than a change to the style pass. It
  is driven from `FlexGridItemBlockification.Generate`, the same main-repo entry point
  `DisplayContentsBoxes` is driven from and for the same reason, and it is idempotent, so adding the
  direct parser call later is a no-op.
  <br>**The second half was a double-count, not a placement error.** With the box placed, a `<div>`
  under a `<g transform="translate(100,50)">` reported itself 100,50 *further on* than the
  `<foreignObject>` containing it: the bridge's `ApplyTransformChain` walks DOM ancestors, and above a
  `<foreignObject>` those are SVG elements whose `transform` is a user-space mapping already folded
  into the box's position. The chain now stops at that boundary.
  <br>**One source of truth for the translate rule.** The bridge's `TryParseSvgTranslate` moved to
  `SvgForeignObjectBoxes.TryParseLoneTranslate` and both callers share it, so the element's own rect
  and its content's cannot disagree about which offsets counted.
  <br>**Bounded on purpose.** The viewport mapping modelled is the identity — one user unit is one
  CSS pixel. A `viewBox` that maps user space is not: its scale is a function of the viewport's
  *used* size, which a style-phase pass does not have. Under one the content keeps no box, exactly as
  before, rather than a confidently wrong placement; the same holds inside a nested `<svg>` viewport,
  whose own box position is not SVG-accurate to begin with. That remainder is stated in
  [open](broiler-js-gaps-open.md#cssom-fonts-svg-and-js-visible-layout-algorithms) and pinned by a
  test, so closing it is a deliberate change.
  <br>**Evidence.** 7 regressions in `SvgForeignObjectContentTests`, every expectation Chromium's
  measured answer to the same markup from one probe run against both — placement at the element's
  corner, ordinary stacking of two block children, an accumulated `translate()` chain, hit testing
  descending into the content, percentage geometry against the viewport, the shape siblings and the
  viewport's own box left unchanged, and the `viewBox` remainder pinned as it stands.
  `GoogleSearchPolyfillTests.Document_HitTesting_Uses_Svg_Groups_Images_ForeignObject_And_Translate`
  and its twin `Broiler.Wpt.Tests` assertion
  `Wpt_CssomView_ElementFromPoint_Uses_Svg_Groups_Images_ForeignObject_And_Translate` both go
  red → green — the same two-for-one the SVG `elementFromPoint` fix below had for its pair.
  Whole-suite diffs against a same-container baseline show no regression in any of the three:
  `Broiler.Layout.Tests` 1317/1317; `Broiler.Cli.Tests` 39 distinct failures → 38, the difference
  being exactly that test; `Broiler.Wpt.Tests` 52 → 51, likewise.
  <br>**What it did not close.** The same roadmap bullet also recorded a computed `display` of
  `inline` rather than `block` for the content, and attributed it to the missing box. That
  attribution was wrong and the box did not change it: a plain `<div>` in the body answers `inline`
  too. It is a general `getComputedStyle` gap — the JS binding does not consult
  `ApplyUserAgentDisplayDefaults` — and is now stated as its own item in
  [open](broiler-js-gaps-open.md#cssom-fonts-svg-and-js-visible-layout-algorithms).

- **An atomic inline-level box was left at the top of its line instead of standing on its
  baseline.** **Fixed, and live** — `Broiler.Layout` is main-repo, so no patch.
  <br>**The gap this was filed as does not exist.** It was recorded as the second of two SVG layout
  gaps — "an inline `<svg>` root is not placed in normal flow against its siblings: two stacked
  `<svg>` elements both report `top: 0` instead of the second clearing the first" — and the test
  pinning it,
  `GoogleSearchPolyfillTests.Document_HitTesting_Keeps_Inline_Svg_Roots_In_Normal_Flow`, expected
  `0|98`. Measured against Chromium at Broiler's own viewport width, both halves are wrong. An
  outermost `<svg>` is inline-level, so the two roots sit **side by side on one line** in every
  browser — they do not stack, and the expected `98` describes a block-stacking model nothing
  implements. Broiler already put them side by side, at the same coordinates Chromium does, and the
  hit results already agreed. Exactly one number differed: the shorter root's `top`, `0` where
  Chromium says `42`.
  <br>**Cause, and it is not about SVG.** CSS2.1 §10.8.1 gives an *atomic* inline — an
  `inline-block` with no in-flow line box, or one whose `overflow` is not `visible` — a baseline at
  its **bottom margin edge**, so two of different heights on one line come out bottom-flush.
  `CssLineBox.SetBaseLine` implemented that for `<img>` and returned early for an `inline-block`
  using the initial `vertical-align: baseline`, on a premise written into the comment beside it:
  that an inline-block's flow position is already on the baseline. It is not — the flow puts it at
  the top of the line, exactly like an image. So every atomic inline-block was top-aligned. An
  inline `<svg>` is one of them only because the parser gives it `display: inline-block` and
  `overflow: hidden`; a plain empty `<span style="display:inline-block">` measured identically
  wrong, which is what showed the gap was in line layout rather than in SVG.
  <br>**Fix.** `CssBox.UsesBottomMarginEdgeBaseline` names the spec's two conditions, and
  `ApplyVerticalAlignment` bottom-aligns the atomic inlines on a line to the tallest of them,
  margin boxes included. `SetBaseLine` then moves the box with `OffsetTop` so its descendants go
  with it. An `inline-block` that lays out its own text is deliberately untouched: its baseline
  comes from its last line box, which this engine does not track.
  <br>**Why the alignment target is the tallest atomic inline and not the line's baseline.** The
  spec's answer is the line's baseline, and using it was tried first. It moves boxes *down* onto a
  strut baseline this engine computes without the half-leading `line-height` contributes, so a
  `font-size: 100px; line-height: 1` line put its baseline about a quarter of the font height too
  low and dragged the box down with it — `NativeAnchorInlineCbPipelineTests` and
  `NativeAnchorAbsInlineCbPipelineTests` both caught that, and Chromium leaves the box at the line
  top there. Adding the half-leading term was tried too and did not reach the case (the owning
  block's `ActualLineHeight` is `normal` on that line). Aligning the atomic inlines to each other
  fixes what is measurably wrong without resting on a number that is not yet right: it can only
  move a box down onto a taller neighbour, and a line with one atomic inline or none is untouched.
  The strut half-leading is a separate defect and is not fixed here.
  <br>**Evidence.** Nine regressions in `AtomicInlineBaselineTests`, every expectation Chromium's
  measured answer to the same markup at the same viewport width — the two `<svg>` roots, the same
  pair as plain inline-blocks, equal heights unmoved, three boxes all aligning to the tallest, an
  explicit `vertical-align` still winning, and a border and a bottom margin each counted into the
  aligned box. One pins the known limit above. The mis-stated test is retargeted to Chromium's
  answer, `42|0|180|230|50|secondRect|secondRect|secondSvg`, with the probe moved onto the shape so
  it still exercises hit-test descent, and it passes. `Broiler.Layout.Tests` 1317/1317; the
  `Broiler.Cli.Tests` failure list is byte-identical to the pre-change baseline with that one test
  moving from failing to passing; the WPT pixel suite is unchanged at 101 passed / 34 failed with
  the same failure list.

- **Writing-mode `scrollIntoView` mapped the block axis onto nothing.** **Fixed, and live** — main
  repo, so no patch.
  <br>**Cause, and it was not the axis mapping.** The block/inline → physical mapping was already
  right; what was missing sat under it. The scrollable-overflow measurement only ever looked toward
  larger physical coordinates — a descendant's `Right`/`Bottom` against the padding box's
  `Left`/`Top`. That is the whole story for an axis that grows that way, and it found nothing at all
  for one that does not: a `vertical-rl` block axis runs right-to-left and its content overflows to
  the **left**, so `scrollWidth` came back equal to `clientWidth`. With no extent there was no range,
  `GetScrollBounds` collapsed to `[0, 0]`, and every block-axis `scrollIntoView` in a vertical
  writing mode clamped to zero.
  <br>**Fix.** A reversed axis measures **both** ways and takes the larger, rather than simply
  measuring the other way — because this engine's layout is not consistent about which side it
  mirrors: `direction: rtl` in a horizontal writing mode lays the overflow out to the right (the
  bridge mirrors it later, in the scroll-coordinate conversion) while `vertical-rl` lays it out to
  the left. Measuring only the reversed side fixes the second and breaks the first; that was tried,
  and the whole-suite diff caught it. Taking the larger is right whichever side the engine chose and
  cannot regress a forward axis, which is measured exactly as before. The "is this axis reversed"
  predicate now has one owner shared with `GetScrollBounds`, since the two disagreeing is what the
  bug was.
  <br>**On not trusting the failing test.** The existing assertion's expected values matched neither
  Chromium nor Broiler when this started, which normally means a stale pin — so rather than code to
  it, a *clean* probe was built (target inside the oversized content, so both engines agree) and both
  engines measured on it. That gave a trustworthy oracle and the root cause; the original test then
  turned out to be a correct pin of the mapping after all, and passes untouched. Its construction —
  an absolutely positioned target overflowing opposite to the content — is simply one the two engines
  legitimately answer differently, which is why it could not be used to tell a mapping bug from a
  layout difference.
  <br>**Evidence.** 29 regressions in `ScrollWritingModeGeometryTests` covering the extent, the range
  sign, and the mapping across four writing modes and both directions; every value is Chromium's
  measured answer, and 9 of the 29 fail before the change. Four tests go red → green across two
  projects: `ScrollIntoView_Maps_Block_And_Inline_Axes_For_WritingModes` and
  `Element_ScrollOffsets_Clamp_And_Respect_WritingMode_Direction` in `Broiler.Cli.Tests`, and
  `Wpt_CssomView_ScrollIntoView_Maps_WritingMode_Block_And_Inline_Axes` and
  `Wpt_CssomView_ScrollLeftTop_WritingMode_Direction_Signs_Are_Clamped` in `Broiler.Wpt.Tests`.
  Whole-suite diffs against same-container baselines show no regressions in either.
- **A frame's mutated scroll state never reached the serialized markup.** **Fixed, and live** —
  main repo, so no patch.
  <br>**Cause, and there were two.** A nested browsing context's document is severed from the main
  tree, and the serialization pre-pass that records scroll offsets walks the main document element —
  so it never reached a frame at all. The offset itself was always recorded correctly: the frame's
  `scrollTop` read back exactly what `scrollIntoView` had set. It simply never reached the markup, so
  a capture showed every frame at its initial scroll position. The pass now runs once per
  materialised content document, which is sound for the same reason the top-layer passes already
  re-run per frame — everything it reads is per-element or per-document (the recorded offset, and a
  computed `overflow` resolved by that document's own style scope) and it needs no geometry, the one
  thing this bridge measures only for the main frame. The visual-viewport scale is deliberately not
  applied inside a frame: pinch zoom scales the frame's box as a whole, so scaling the offset it
  scrolled *within* itself would count the same zoom twice.
  <br>**The second cause was the test.** It asserted the `position: relative; top: -160px` wrapper
  the pass used to bake, which had been deliberately retired in favour of handing the offset to the
  layout engine as a data attribute — no wrapper div, no inline position writes, no fixed-descendant
  reparenting. So it was red for two independent reasons and fixing only the behaviour would have
  left it red. It now pins the form the top-level document produces for the identical mutation, and
  checks it against the top level in the same run, so the two cannot drift apart again. This is the
  [xUnit status](xunit-suite-status.md) doc's "deliberate change landed, the test pinning the old
  behaviour left behind" category — worth separating from the real gap rather than treating the
  whole failure as either one.
  <br>**Evidence.** `ScriptEngineExecuteTests.DomBridge_SerializeToHtml_Preserves_Mutated_Iframe_Scroll_State_In_SrcDoc`
  red → green, and still red with the source fix stashed and only the test edit in place — which is
  what shows the behaviour, not the assertion, is what closed it. Whole-suite diffs against a
  same-container baseline: `Broiler.Cli.Tests` no regressions, `Broiler.Wpt.Tests` unchanged.
- **SVG `elementFromPoint`.** **Fixed, and live** — this one is in the main repo, so unlike track 1's
  and track 3's engine fixes it needs no patch and CI sees it.
  <br>**Cause.** An SVG child is not in the CSS box tree, so nothing below the `<svg>` root had a
  rect at all: `getBoundingClientRect` answered `0,0,0,0` for every shape, and hit testing — which
  asks each element for a rect and skips anything empty — could never descend past the root.
  `document.elementFromPoint` over a `<rect>` returned the `<svg>`. The group path that already
  existed (a `<g>`'s rect is the union of its children's) could not help, because it had nothing to
  union.
  <br>**Fix.** A shape's client rect is resolved from its own geometry attributes, composing three
  mappings outermost-first: the viewport's rendered origin from the box tree, the `viewBox`
  transform, and the accumulated `translate()` of the ancestor `<g>` chain. One entry point serves
  both `getBoundingClientRect` and hit testing, so the two cannot disagree about where a shape is —
  wiring only the hit-test half would have left `elementFromPoint` finding a shape whose own
  `getBoundingClientRect` still said zero.
  <br>**Bounded on purpose.** `rect`, `image`, `foreignObject`, `circle`, `ellipse`, `line`,
  `polyline` and `polygon` resolve exactly. `path` and `use` do not — a path needs the curve and
  `use` needs its referent — and report no rect, which is what every shape did before, rather than a
  confidently wrong one. Only `translate()` is accumulated; any other transform function leaves its
  subtree untranslated rather than having some functions applied and others dropped.
  `preserveAspectRatio` is modelled at its default. Each gap is pinned by its own test.
  <br>**Evidence.** 17 regressions in `SvgShapeGeometryTests`, every expectation Chromium's measured
  answer to the same markup from one probe run against both — including the two `viewBox` cases that
  separate a plausible formula from the real one (a viewport whose aspect differs from the box's, and
  a non-zero `min-x`/`min-y`). `GoogleSearchPolyfillTests.Document_HitTesting_Uses_Svg_Viewports_And_Rect_Geometry`
  and its twin `Broiler.Wpt.Tests` assertion
  `Wpt_CssomView_ElementFromPoint_Uses_Svg_Viewport_And_Rect_Geometry` both go red → green, which is
  the two-for-one the [xUnit status](xunit-suite-status.md) predicted for this pair. Whole-suite
  diffs against a same-container baseline show no regression in either project.
  <br>**What it did not close,** and why: two other SVG hit-test assertions still fail, each now for
  exactly one reason and both of them *layout* rather than scripting — `foreignObject` content is not
  laid out, and an inline `<svg>` root is not placed in normal flow. Both are stated in
  [open](broiler-js-gaps-open.md#cssom-fonts-svg-and-js-visible-layout-algorithms).
- **The `document` surface that names the document's own contents was half missing and half the
  wrong kind of object.** A probe of ~30 document properties against Chromium, run to decide whether
  `document.doctype` was a standalone item, returned one coherent cluster instead.
  <br>**Absent outright:** `anchors`, `embeds`, `plugins`, `doctype`, `dir` and `designMode`. Each
  read `undefined`, so the ordinary `document.embeds.length` was a `TypeError` rather than `0`.
  <br>**Present but the wrong kind of object:** `forms`, `images`, `links`, `scripts` and
  `styleSheets` were plain `Array`s rebuilt on every read. That is wrong three ways at once, and only
  the first is loud. An array is a **snapshot**, so `var f = document.forms; addForm(); f.length` did
  not move; it carries `map`/`filter` but not `item`/`namedItem`, the opposite of a browser in both
  directions, so feature detection branched wrongly either way; and a fresh object per read made
  `document.forms === document.forms` **false**, where a browser hands back one cached object per
  document. For `plugins` that identity is not a nicety but HTML §3.1.5's literal requirement — it
  must return *the same object* `embeds` does.
  <br>**`doctype` was the odd one: the node was already there.** The parser has produced a canonical
  `DomDocumentType` and appended it as the document's first child for some time, and
  `document.firstChild` returned it. Only the accessor DOM §4.5 names was missing — and
  `document.childNodes` filtered to elements, so the same node was reachable by position and
  invisible by name, with `childNodes[0]` and `firstChild` disagreeing about what the first child
  was.
  <br>**Fixed** by giving each collection the `HTMLCollection` machinery track 6 action 1 built —
  live contents over a filter of the document's element list, Web IDL indexed and named access — plus
  CSSOM's `StyleSheetList` (§6.1) for `styleSheets`, which is neither of the node collections and
  gained its interface here. Each collection object is built once and closed over, so identity holds
  and `plugins`/`embeds` are one object. `document.childNodes` became a live `NodeList` over every
  child rather than an element-filtered array. `dir` reflects the document element's attribute
  *limited to known values* — the getter answers a canonical keyword or the empty string while the
  setter writes through unchanged, so `document.dir = 'LTR'` reads back `"ltr"` over an attribute
  still spelled `LTR`; `designMode` is an enumerated state that ignores an unrecognized assignment
  rather than storing it.
  <br>**Two behaviours worth naming, because reasoning alone gets them wrong.** `links` and `anchors`
  are not two names for one set: `links` is `a`/`area` *with an `href`* and `anchors` is `a` *with a
  `name`*, so a page's anchors can land in one, the other, or neither. And the named getter is a
  single pass testing both `id` and `name`, not all ids and then all names — DOM §4.2.10.2 asks for
  the first element for which *at least one* is true, so over
  `<form id=b><form name=a id=c><form name=b>`, `document.forms.b` is the first form. Chromium
  agrees with both.
  <br>Two existing `NodeMutationBindingModuleTests` characterizations counted `document.childNodes`
  under the old element-only assumption and were re-taken from Chromium: a page with a `<!DOCTYPE>`
  has two document children, and `replaceChildren` clears the doctype along with everything else, so
  `document.doctype` is `null` afterwards.
  <br>Main-repo `Broiler.HtmlBridge.Dom` fix (`Features/DocumentCollectionBinding.cs`,
  `Features/DomCollectionBinding.cs`, `Features/NodeMutationBinding.cs`,
  `DomBridge/Registration/DocumentSurface.cs`); regressions in `DocumentCollectionSurfaceTests`.
  What the same audit found and did *not* fix — `document.all`, which needs an engine capability — is
  in [open](broiler-js-gaps-open.md#dom-interface-and-collection-model). The sub-documents' own older
  accessors, the other thing it deferred, are the entry below.
- **A frame's `document` answered a different object model from the document containing it.** The
  fix above was deliberately kept to one document, so an `<iframe>`'s `contentDocument` — and every
  `createDocument`/`createHTMLDocument` result, which is a sub-document too — went on building its
  collections in `SubDocumentBinding` as the `JSArray` snapshots the main document had just been
  moved off. From one page, asking the two documents the same question got two answers:
  `d.forms.constructor.name` was `"Array"` against the parent's `"HTMLCollection"`,
  `d.forms === d.forms` was **false**, `namedItem` and named access did not exist,
  `var f = d.forms; d.body.appendChild(d.createElement('form')); f.length` did not move, and
  `anchors`, `embeds`, `plugins`, `doctype`, `dir` and `designMode` were absent outright — so a
  frame's script hit the same `TypeError` on `d.embeds.length` the main document had stopped giving.
  The query methods went the same way: `getElementsByTagName`, `getElementsByClassName`,
  `getElementsByName`, `querySelectorAll` and `childNodes` were all arrays, so a frame's script got
  `map`/`filter` a browser does not offer on any of them and no `item` it does.
  <br>Nothing about a frame's document makes it a different kind of document, and a script inside one
  is a script like any other. Chromium answers every one of these identically for both.
  <br>**Fixed** by projecting a sub-document onto `IDocumentCollectionHost` — the contract
  `DocumentCollectionBinding` already consumes — so both documents are served by one implementation
  rather than two. Only two of the contract's members are genuinely per-document: the element list,
  which becomes this root's sub-tree, and `currentScript`, which a sub-document does not track.
  Wrapper identity and the two stylesheet services are per-*node* questions the bridge answers the
  same way whichever document asks, so they delegate straight through, and the bridge's own
  `BuildStyleSheetsCollection` — the last builder that returned a `JSArray` where CSSOM §6.1 requires
  a `StyleSheetList` — was deleted rather than left as a second answer. The query methods were given
  the types DOM assigns them, matching the main document down to which one is *not* live:
  `querySelectorAll` stays a static `NodeList` (§4.2.6, the one collection specified as a snapshot)
  while `getElementsByTagName`/`ByClassName` are live `HTMLCollection`s and `getElementsByName` a
  live `NodeList`.
  <br>**`doctype` needed the node before it needed the accessor,** which is the reverse of the main
  document, where the node was already there and only the name was missing. A frame's tree never
  carried one: `BuildDocumentTree` returns the `<html>` element alone, so a resource declaring a
  DOCTYPE produced `childNodes` of `[<html>]` where the containing document's is `[doctype, <html>]`,
  and the accessor would have had nothing to find. The frame parse now appends it first, through the
  same `ParseDocType` reading `document.write` was already using for exactly this. It is invisible to
  everything that walks a sub-document by element — `GetDocumentElement` and the frame serializer both
  filter to `DomElement`, and a `DomDocumentType` has not been one since Phase 4 item 1.
  <br>`designMode` is per-document state (HTML §3.2.7), so each sub-document carries its own rather
  than sharing the containing document's — pinned, because a shared field would have been the easy
  wrong answer.
  <br>Main-repo `Broiler.HtmlBridge.Dom` fix (the new `Features/SubDocumentCollectionHost.cs`,
  `Features/SubDocumentBinding.cs`, `Features/ISubDocumentHost.cs`, `DomBridge.SubDocumentHost.cs`,
  `DomBridge/StyleSheets.cs`, `DomBridge/SubDocuments.cs`); regressions in
  `SubDocumentCollectionTests`, each of which asks the frame *and* the containing document the same
  question in one page, because the defect was never a frame being wrong in the abstract — it was the
  two disagreeing about what a document is.
- **`setAttribute` accepted any name and `querySelector` accepted any selector.** The last two
  members of the DOMException family, and the second was worse than "returns `null`" — it returned
  the *wrong element*. The lenient matcher read `div:::bogus` as `div` and handed back a real node,
  and `[` matched four; so an invalid selector did not fail, it quietly succeeded at something else,
  which no caller can detect. `setAttribute` wrote every invalid name through — `@click`, `foo bar`,
  `1abc`, `-x` all became attributes a browser refuses to create — and the one name that did fail,
  the empty string, threw a bare `Error` carrying no `name` and no `code` to branch on.
  <br>**Fixed** at the scripted-DOM boundary, in the main repo, and that placement is the design
  rather than a convenience. The two obvious homes are exactly where the rules must *not* go, for the
  same reason in both cases: `Broiler.Dom`'s `DomElement.SetAttribute` is what the HTML parser calls,
  and HTML permits attribute names the XML `Name` production rejects; `Broiler.CSS.Dom`'s
  `CssSelectorMatcher.Matches` is what the cascade calls, and a rule whose selector does not parse is
  *dropped* per CSS error handling, never fatal. Throwing in either place would break the layer whose
  job is to tolerate bad input. The requirement belongs to the scripted API, so it lives where the
  `DOMException` is already minted — which also means **no submodule patch**, so it is live in CI
  now rather than waiting to be applied.
  <br>**Every expectation was measured against Chromium**, not derived from the grammar, and that
  caught two assumptions wrong in opposite directions: `[tabindex=0]` *is* a syntax error (an
  unquoted attribute value must be an identifier and a digit cannot start one) while
  `setAttribute('a:b:c', …)` is perfectly valid, because `Name` admits colons. The second one was
  the dangerous one — reusing the element-name rule, which deliberately forbids colons, would have
  started rejecting `xlink:href` and broken inline SVG. Over a 149-case corpus run against both
  engines the two now agree on 143; all six divergences are Broiler *accepting* where Chromium
  throws, and none is the reverse, which is the only safe direction for a change that turns silence
  into an exception.
  <br>**The six are two deliberate decisions.** A well-formed but unknown pseudo (`:nope`,
  `::bogus`, `::-moz-focus-inner`, `:matches()`, and a pseudo-class after a pseudo-element) is
  accepted, because rejecting one needs a list of every pseudo this engine supports and such a list
  drifts against what pages use rather than against the specification — Chromium itself accepts
  `:focus-visible`, `:defined`, `::marker` and `::-webkit-scrollbar` while rejecting
  `::-moz-focus-inner`. Turning an unknown name into a throw would break a page that merely asked
  for a pseudo Broiler lacks. The other is the Selectors 4 `s` case flag, which is valid per
  specification and which Chromium has not implemented.
  <br>**The pseudo-element half was found by this work and fixed with it.** A selector carrying a
  pseudo-element selects a box, not an element, so it matches nothing through the DOM API — but the
  matcher strips the pseudo-element and matches what is left, so `querySelector('::before')` returned
  the `<html>` element. That is the same silently-wrong-element failure by another route, so
  `querySelector`/`querySelectorAll`/`matches`/`closest` now answer no element for any selector
  carrying one, in both the `::` and the legacy one-colon spellings, while the cascade goes on
  applying `::before` rules — pinned, since a rule that reached the renderer would stop generated
  content painting. A pseudo-element that is not the subject (`div::before p`) is a `SyntaxError`
  instead, which is what a browser gives.
  <br>`toggleAttribute` and `setAttributeNS` validate too, because a browser validates from them;
  `getAttribute`, `hasAttribute` and `removeAttribute` deliberately do not, because a browser accepts
  an invalid name from all three — they ask about a name rather than create one. Both halves of that
  line are pinned.
  <br>Main-repo `Broiler.HtmlBridge.Dom` fix (the new `Features/DomApiSyntax.cs`,
  `DomBridge/Utilities.NameValidation.cs`, `DomBridge/Utilities.cs`, `Features/AttributesBinding.cs`,
  `Features/SelectorsBinding.cs`, `Features/DocumentQueryBinding.cs`,
  `Features/SubDocumentBinding.cs`); regressions in `DomApiSyntaxTests`.
  <br>**What this narrowed but did not close:** an unknown pseudo-class *with an argument* still
  matches the first element rather than nothing, so `querySelector(':matches(a)')` answers `<html>`
  where the argument-less `:nope` already answers `null`. The cause is the matcher's lenient default
  arm in `Broiler.CSS.Dom`, a matching question rather than a syntax one; it is characterized in
  `DomApiSyntaxTests` and left in
  [open](broiler-js-gaps-open.md#dom-interface-and-collection-model).
- **Element, attribute and document wrappers now name their interfaces, and the interfaces inherit.**
  The non-element wrappers were linked first; elements were left deliberately, because an element's
  interface is a tag question the engine's own table could not answer. It carried an overlapping
  `("HTMLMediaElement", "audio video")` entry beside `HTMLAudioElement` and `HTMLVideoElement`, so
  `audio` named two interfaces and a reverse lookup had none — and a tag the table omitted had to
  fall back to something a browser splits three ways. Guessing between them would have put a *wrong*
  name where an honest `"Object"` is at least not misleading, which is why it stayed open rather than
  being approximated.
  <br>**It was measured instead of guessed.** Every HTML tag was run through Chromium's own
  `document.createElement(tag).constructor.name`, and the table rebuilt from the result: single-valued,
  with the abstract bases moved into an inheritance list. Over the full tag corpus Broiler and
  Chromium now agree on every case. Three of them are ones reasoning gets wrong — `plaintext` is
  plain `HTMLElement`, not the `HTMLPreElement` it sat under with `listing`/`pre`/`xmp`; a
  hyphenated unknown name (`x-foo`) is an `HTMLElement`, because it is a valid custom element name
  even undefined; and a tag removed from HTML (`applet`, `keygen`) is `HTMLUnknownElement` even
  though the parser still knows the name.
  <br>**The measurement found a live bug in the shipped `instanceof` table.** `plaintext` was grouped
  under `HTMLPreElement`, so `document.createElement('plaintext') instanceof HTMLPreElement` answered
  `true` where a browser answers `false`. That is fixed with the same edit, and pinned.
  <br>**The interfaces now inherit along the chain Web IDL gives them** —
  `HTMLDivElement → HTMLElement → Element → Node → EventTarget`, and
  `HTMLAudioElement → HTMLMediaElement → …` — which is the part that is not cosmetic. Extending an
  interface prototype is the ordinary polyfill idiom, and `Element.prototype.matches = …` now reaches
  every element where the assignment used to go to an object nothing inherited from. The chain is
  built with `setPrototypeOf` rather than a fresh `Object.create`, so each prototype keeps its
  identity and its non-enumerable `constructor` — pinned, because a `for...in` over an element that
  started yielding `constructor` would be a silent regression in every enumeration a page does.
  <br>Making the table single-valued carried its own risk, and it is pinned too: `audio` no longer
  names `HTMLMediaElement`, so `audio instanceof HTMLMediaElement` has to come from the inheritance
  edges instead. Each interface's `instanceof` set is expanded at registration to its own tags plus
  every descendant's, so an abstract base answers for tags that never mention it.
  <br>`document` and attribute nodes needed their own links: neither is minted at the node choke
  point where every other wrapper is linked. `HTMLDocument` was an interface the engine did not
  register at all. The document's link also has to run *after* the polyfill pass that registers the
  constructors, not where the document is built — the same ordering the lazy document collections are
  built around — and the wrappers minted eagerly during attach (`document.documentElement` is
  materialized as a value property, so the `<html>` wrapper is always one) are re-linked by sweeping
  the wrapper registry at that point rather than by naming that one case.
  <br>Main-repo `Broiler.HtmlBridge.Dom` fix (`DomBridge/Utilities.DomInterfaces.cs`,
  `DomBridge/WrapperPrototypes.cs`, `DomBridge/Registration/Registration.cs`,
  `Features/AttributesBinding.cs`, `Features/SubDocumentBinding.cs`); regressions in
  `DomInterfacePrototypeTests`, and `WrapperInterfacePrototypeTests`' own
  "element and attribute are still unlinked" assertion updated — that fixture asked for a deliberate
  update rather than a silent flip, and this is it.
  <br>**What this leaves:** the engine's members are still own properties of each wrapper rather than
  living on the prototypes, so an interface prototype carries nothing of its own; SVG elements report
  the base `SVGElement` because no per-tag SVG interfaces are registered to point at; and
  `NamedNodeMap` was unregistered, so `element.attributes` reported `"Object"` — that last one is the
  entry below. The other two are in
  [open](broiler-js-gaps-open.md#dom-interface-and-collection-model).
- **`element.attributes` is a live `NamedNodeMap`, and an attribute is one `Attr` node with a live
  value.** It was a fresh plain object per read, carrying the same four faults the document
  collections had before they moved onto the shared collection machinery — and the fourth is the one
  that made a page *throw* rather than answer wrongly.
  <br>**No interface:** `constructor.name` was `"Object"`, and the bare name `NamedNodeMap` was a
  `ReferenceError`, which aborts the whole script that names it rather than the expression that did.
  **No identity:** `el.attributes === el.attributes` was `false`. **No named access:**
  `el.attributes.id` was `undefined`, where DOM §4.9.1 makes a qualified name a supported property
  name. **And half-live:** `length` was a getter over the current attributes while the indices were
  materialized once at build time, so a map held across a `setAttribute` reported the new count with
  nothing at the new index — the idiomatic `for (i = 0; i < m.length; i++) m[i].name` read
  `undefined.name` and threw.
  <br>**Fixed** by building it through `DomCollectionBinding`, the same machinery `NodeList`,
  `HTMLCollection` and `StyleSheetList` already use, so both halves come from one contents function
  and cannot disagree again. The members that mutate or that need the owning element cannot be
  written against `this.length` and `this[i]` the way every other method there is, so they are host
  functions — but still on the *prototype*, shared, each finding its element from a weak table keyed
  on the receiver, so no per-instance slot appears and `Object.getOwnPropertyNames` stays the indices
  alone. Calling one on a foreign object is a `TypeError`, as a browser gives.
  <br>**`getNamedItem` cannot be `this[name]`,** which is how `HTMLCollection.namedItem` is written
  and was the first attempt here. An interface member wins the property lookup over a named one, so
  an element carrying `length="x"` has `attributes.length === 3` — correctly — while
  `getNamedItem('length')` must still hand back the attribute. Measured; it is a host function for
  that reason and the case is pinned.
  <br>**The `Attr` nodes had to become real objects for any of this to mean anything.** A cached map
  over freshly minted attribute nodes would still answer `el.attributes[0] === el.attributes[0]`
  false, and a cached *node* holding a snapshot value would be worse than the old per-read one — the
  single surviving object would report whatever the value was when first asked. So an attribute is
  now one node per element and name across every access path (index, qualified name, `getNamedItem`,
  `getAttributeNode`), its `value` reads through to the element and writing it writes back, and
  removing the attribute detaches the node: it keeps the value it had, its `ownerElement` becomes
  `null`, and re-adding mints a new node rather than reviving the old one. All measured, and the
  detachment is observable — the old node and the new one report the old and the new value.
  <br>**Two existing fixtures were measured wrong and are corrected.**
  `Element_SetAttributeNode_Replaces_And_Returns_Old_Attr` asserted that re-setting an element's own
  attribute node returns something reading the *previous* value, which only held while an `Attr` was
  a per-read snapshot: a browser has one node with a live value, so after `attr.value = 'new'` the
  returned node **is** `attr` and reads `'new'`. Replacing with a genuinely *different* node is the
  other half and does detach the displaced one — DOM §4.9.2 distinguishes the two, and both are now
  pinned from measurement.
  <br>**One thing this corrected beyond its own scope:** the collection prototypes' members were
  non-enumerable, on `NodeList` and `HTMLCollection` as well, where Web IDL makes an interface's
  members enumerable and a browser agrees — `for (var k in el.childNodes)` yields `item`, `forEach`
  and the rest beside the indices. One word in the shared `define` helper; verified against both
  suites.
  <br>Over the two measured corpora Broiler and Chromium now agree on every attribute-node case and
  on every map case but one: `for...in` omits `length`, because it is answered by the host rather
  than held as a prototype accessor, and the member order is definition order rather than Web IDL's.
  Recorded rather than papered over.
  <br>Main-repo `Broiler.HtmlBridge.Dom` fix (`Features/DomCollectionBinding.cs`,
  `Features/AttributesBinding.cs`, `DomBridge/Utilities.DomInterfaces.cs`); regressions in
  `NamedNodeMapTests`.
- **Custom Elements have a production implementation.** There was none: `customElements` was
  undefined and `HTMLElement` threw `Illegal constructor`, so `class X extends HTMLElement` followed
  by `customElements.define(…)` failed on the bare name — which aborts the whole script, not the
  statement that named it. The WPT runner carried a shim to get past that, and the shim could not
  reach what mattered: its `HTMLElement` handed back a plain element that did not carry the class's
  prototype, so a component's own methods were unreachable and the four reaction callbacks had to be
  copied onto each instance by hand.
  <br>**The one piece that had to be JavaScript is the base constructor**, and the reason is
  `new.target`. `new X()` runs `X`'s constructor, which calls `super()`, and only `new.target` says
  which subclass is being built — so which prototype the element takes and, through the registry,
  which tag name it has. A host function cannot see it: the engine's `Arguments` does not carry one.
  So the base reads it in JavaScript and calls the host for the element, and everything else — the
  registry, name validation, upgrades, reaction dispatch — is C#, where the DOM is. That also kept
  the whole feature in the main repo, with no submodule patch.
  <br>Two things the earlier interface work put in place are what make the base work at all.
  Returning an object from a base constructor makes it `this`, so the subclass constructor runs
  against a real DOM element; and because an element's members are its *own* properties, re-pointing
  its prototype at `new.target.prototype` adds the class without displacing any of them, while the
  class's chain still ends at the `HTMLElement.prototype` every element wrapper is linked to.
  <br>**Upgrading reuses the same constructor path rather than a second one**, which is what the
  specification's construction stack is for: an upgrade pushes the existing element and the base's
  callback hands that one back instead of minting a new one, so the author's constructor runs against
  the node already in the tree. The shim instead copied attributes and children onto a fresh element
  and swapped it in, which loses node identity — a page holding the element from before the
  definition landed kept pointing at the discarded one. Identity across an upgrade is pinned.
  <br>**Reactions come off the canonical `DomDocument.Mutated` stream, which is synchronous.** The
  obvious reuse would have been `MutationObserver`, which already subscribes there — but its delivery
  is a microtask, and a browser runs `connectedCallback` before the statement after the `appendChild`
  that caused it. A component that reads its own DOM immediately after inserting itself would have
  seen nothing.
  <br>**The runner's shim now steps aside, and finding out why it did not is part of this.** Its
  capability probe asked whether `new HTMLElement()` yields an element — which a *correct*
  implementation can never satisfy, because a bare `new HTMLElement()` has no definition to build
  from and must throw, exactly as in a browser. So the probe failed against the real implementation
  and the shim kept winning, which would have meant WPT never exercising the production code at all.
  The probe now asks whether a *defined* class constructs an element, which is the capability the
  shim exists to fake.
  <br>Over a 17-case corpus run against both engines, Broiler and Chromium agree on every one:
  construction, `createElement` running the definition's constructor, name validation and the
  reserved hyphenated SVG/MathML names, duplicate name and constructor rejection,
  `get`/`getName`/`whenDefined`, upgrade-in-place with identity, the connected, disconnected and
  attribute-changed reactions, the attributes an upgrade replays, and an undefined hyphenated tag
  staying a plain `HTMLElement`.
  <br>Main-repo fix (the new `Features/CustomElementsBinding.cs`, `Features/ICustomElementsHost.cs`,
  `DomBridge.CustomElementsHost.cs`, `DomBridge/Registration/CustomElements.cs`, plus
  `Features/DocumentFactoryBinding.cs` and `src/Broiler.Wpt/WptTestRunner.cs`); regressions in
  `CustomElementsTests`.
  <br>**Left out deliberately, and not faked:** customized built-ins (`extends`/`is=`), which
  `define` rejected rather than accepting and ignoring; form-associated custom elements; and
  `adoptedCallback`. All three have since landed, below — form association among them, so nothing
  from this list is still open. The one reaction that never fires is `formStateRestoreCallback`, and
  deliberately: it reports a value restored by session history or an autofill pass, and this engine
  performs neither. See [open](broiler-js-gaps-open.md#templates-and-shadow-dom), which carries only
  the synthetic-shadow-tree remainder.
- **Customized built-in elements and `adoptedCallback`.** `define` rejected an `extends` option with
  a `NotSupportedError`, so `class Fancy extends HTMLButtonElement` — the idiom for keeping a native
  control's behaviour and adding to it — lost its component at the `define` call, which takes the
  rest of the script with it. `adoptedCallback` never fired, because there was no document-adoption
  path to fire it from: `document.adoptNode` had no implementation at all.
  <br>**Each per-tag interface global is now a real constructor**, because a customized class reaches
  one through `super()`. They are still not directly constructible — without a `new.target`, or
  through a class no definition names, they throw the same `Illegal constructor` they always did —
  and the construction hook is a closure the custom-element registration binds once, so no page-
  reachable global mints elements out of band. The interface a `super()` goes through is *passed* to
  the registry rather than inferred from the definition, which is HTML §4.13.3's active-function-object
  check: without it, `class I extends HTMLButtonElement` registered with no `extends` option would
  silently build an autonomous `<bad-3>` through `HTMLButtonElement`, and an `HTMLElement` subclass
  registered *with* one would silently build a `<button>`. Both are `TypeError`s, with Chromium's
  distinct messages.
  <br>**An element's is value is not its `is` attribute**, and that is the part that could not be
  guessed. An element parsed from `<button is="fancy-b">` has both; `new FancyButton()` and
  `createElement('button', {is: 'fancy-b'})` produce one whose `getAttribute('is')` is `null` and
  which still serializes as `<button is="fancy-b">` — HTML §13.3 writes the is value out so the
  markup re-parses into the same element. An `is` naming nothing defined is kept as well, so a later
  `define` upgrades it. A definition only reaches the tag it extends: a plain `<button>` is untouched,
  and `<div is="fancy-b">` stays a plain `<div>` rather than running a button subclass's constructor
  against something that is not a button.
  <br>**Adoption is the operation a custom element can observe**, which is why `importNode` is not a
  substitute for it: importing copies, so the node the page holds afterwards is a different one.
  `adoptNode` is on the page's document and on a sub-document, and both *directions* are heard —
  adoption publishes its mutation on the document a node moves **to**, so listening to the page's own
  document alone would hear an adoption into the page and miss the symmetric one out of it. Every
  document this bridge mints is subscribed. The whole adopted subtree is reported, not only the node
  named on the record.
  <br>**Two fixes fell out of the same work.** A class statically inherits `@@hasInstance` from the
  interface it extends, so `class Fancy extends HTMLButtonElement` answered the interface's *tag*
  test and reported every `<button>` on the page as one of its own instances; a subclass now gets the
  ordinary prototype-chain answer. And the connected test asked for the page's document specifically,
  so an element inserted into a frame's or a `createHTMLDocument`'s tree was never connected — a
  browser runs its `connectedCallback` there, measured as connected, disconnected, adopted, connected
  over a cross-document `appendChild`.
  <br>Over a 26-case corpus run against both engines, Broiler and Chromium agree on every case but
  one: the parsed element's `outerHTML` orders attributes `id` before `is` where a browser preserves
  source order, which is the bridge's pre-existing serialization order for every attribute and not
  this change's.
  <br>Main-repo fix (`Features/CustomElementsBinding.cs`, `DomBridge/Registration/CustomElements.cs`,
  `DomBridge/Utilities.DomInterfaces.cs`, `DomBridge.CustomElementsHost.cs`,
  `DomBridge.Serialization.cs`, `Features/DocumentFactoryBinding.cs`,
  `Features/SubDocumentBinding*.cs`); regressions in `CustomizedBuiltInElementsTests` and
  `CustomElementAdoptionTests`.
- **Form-associated custom elements** — the last of the three the Custom Elements slice named.
  `attachInternals()` was undefined, so the line every such component's constructor opens with,
  `this.internals_ = this.attachInternals()`, was a `TypeError` that took the constructor down and
  with it the upgrade of every instance on the page. A component could sit inside a form; it could
  not *be* a control.
  <br>**`ElementInternals`, `ValidityState` and `CustomStateSet` are real interfaces**, with their
  members on their prototypes and per-instance state in a weak table — the shape `Range`,
  `Selection` and `Blob` established, and the one Chromium reports (zero own property names on an
  `ElementInternals`). `ValidityState` is keyed into the same table, so `internals.validity` is one
  live object rather than a snapshot. `CustomStateSet` is written in JavaScript over a real `Set`,
  which gets the setlike iteration protocol — `for…of`, `values`/`keys`/`entries`, `forEach` — right
  by construction rather than by re-deriving it through host functions.
  <br>**Every form-related member refuses on an element that is not form-associated**, rather than
  answering an empty or neutral value: `form`, `labels`, `willValidate`, `validity`,
  `validationMessage`, `setFormValue`, `setValidity`, `checkValidity` and `reportValidity` are each a
  `NotSupportedError` naming that reason, while `states` and `shadowRoot` work regardless. Answering
  `null` for `form` there would say "this control has no form" where the truth is "this is not a
  control". `formAssociated` is read as the static it is: an instance getter of the same name
  deliberately does not count, which is measured rather than assumed.
  <br>**Nothing here is a shape-only stub, and making that true needed the form entry list.**
  `new FormData(form)` enumerated the *wrapper's* own string properties, so it produced the element
  object's members — `tagName`, `innerHTML` and the rest — instead of the form's fields. That is also
  the place a browser reads a form-associated custom element's submission value, so `setFormValue`
  would have had nowhere to be observed. The entry list is built properly now (HTML §4.10.21.4), with
  the specified exclusions: a disabled control, a nameless control, an unchecked checkbox or radio,
  and a button that is not the submitter each submit nothing. A `FormData` submission value
  contributes its own entries and the element's `name` is not used; `null` means it submits nothing.
  `setValidity`'s flags are likewise what the owning form's `checkValidity` answers from, and
  `checkValidity` fires the `invalid` event at the element.
  <br>**The three reactions that can fire, do.** `formAssociatedCallback` reports the owner at the
  upgrade that made the element custom — but only when that owner is non-null, which is measured and
  is not the plausible reading — and again on every later owner change; `formDisabledCallback`
  reports a change from the element's own `disabled` attribute *or* an ancestor
  `<fieldset disabled>`; `formResetCallback` reaches the custom controls of a form being reset, which
  is the whole of what a reset can mean for a control with no dirty flags to clear. Both tracked
  states are computed from the tree rather than stored, so they are re-read after each mutation over
  the form-associated elements a page actually upgraded — the alternatives (mapping a fieldset
  mutation to its descendants, an id change to the elements naming it) are the kind of partial
  dependency tracking that silently misses a case.
  <br>**`formStateRestoreCallback` is deliberately never fired.** It reports a value restored by
  session history or an autofill pass, and this engine performs neither; firing it with the value the
  page just set would be an invention rather than a restoration.
  <br>`form.elements` lists a form-associated custom element among its controls, and such an element
  is labelable in its own right — neither is answerable from a tag list, which is why the control
  collection is re-walked in the bridge, where the registry that knows is.
  <br>Over a 36-case corpus run against both engines, Broiler and Chromium agree on every one.
  <br>Main-repo fix (the new `Features/ElementInternalsBinding.cs`,
  `Features/IElementInternalsHost.cs`, `DomBridge.ElementInternalsHost.cs`,
  `DomBridge/FormEntryList.cs`, plus `Features/CustomElementsBinding.cs`,
  `Features/FormAssociationBinding.cs`, `Features/FormBinding.cs`, `Features/FetchBinding.cs`,
  `DomBridge/FormReset.cs`); regressions in `FormAssociatedCustomElementTests`.
- **`ReadableStream` and `FileReader`, and `Blob.prototype.stream()` with them.** Two of track 6's
  capability decisions, taken together because they are one slice: `stream()` was left out precisely
  because it returns a `ReadableStream`, and the engine had only a partial one — the shape-only
  object `response.body` handed back, carrying a `getReader` whose reader had `read`, `cancel` and
  `releaseLock` and nothing else. No `closed`, no `tee`, no `cancel` on the stream, and no
  constructor: `new ReadableStream(...)` was a `ReferenceError`, the kind that aborts the script
  rather than the statement. `FileReader` was absent outright, so the standard way a page turns a
  dropped file into text, a preview data URL or an `ArrayBuffer` was a `ReferenceError` too.
  <br>**The stream is JavaScript, and that is the point rather than a shortcut.** The specification
  is a state machine over promises — a queue, a list of pending read requests, and a pull signal that
  must not re-enter — and writing it in host functions would mean re-deriving the promise plumbing
  the engine already has. It ships as an embedded asset beside the content-rendering polyfills. The
  only thing the host provides is a blob's bytes; that hook is captured into the asset's closure and
  deleted from the global, so a page cannot reach a blob's bytes through it.
  <br>**One stream, three producers.** `blob.stream()`, a fetch body and a page's own
  `new ReadableStream` now hand back the same interface. A fetch body is the case that needed care:
  the Body mixin's `bodyUsed` is "disturbed", and it is reported from the underlying source's `pull`
  rather than from a wrapper on the instance, so the stream a page holds still has no own properties;
  its high-water mark is zero precisely so construction does not pull and mark the body used before
  anything read it. `text()`, `json()` and `clone()` refuse a body that is disturbed *or* locked, and
  the locked half is answered by the stream itself.
  <br>**Not implemented, and detectably so:** `pipeTo`/`pipeThrough`, which need a `WritableStream`,
  and BYOB readers, which need a byte-stream controller, so `getReader({mode: 'byob'})` throws rather
  than handing back a default reader that would ignore the caller's buffer.
  <br>**Async iteration was the third of those and is no longer** — it was blocked on the engine
  rather than on the stream, and that block is gone. `for await` used to leave its async function
  suspended, so installing the hook would have turned the ordinary
  `for await (const chunk of response.body)` from a `TypeError` a page's script survives into a
  capture that never settles. The engine fix landed (track 1, *`for await` over a step result that
  is not already settled*) and `values()`/`@@asyncIterator` went on with it — recorded as its own
  entry in this track below.
  <br>`FileReader` brought `ProgressEvent` with it, which also did not exist: its events are
  `ProgressEvent`s and a handler reads `e.constructor.name` as much as `e.loaded`. The reader's own
  event plumbing is its own, because this realm's `EventTarget` is not subclassable — a deviation
  worth naming: `addEventListener`, `removeEventListener` and `dispatchEvent` are own members of
  `FileReader.prototype` where a browser inherits them.
  <br>Over a 36-case corpus run against both engines, Broiler and Chromium agree on every one: the
  event order and what each event reports, the four conversions (including that a typeless blob reads
  as `data:application/octet-stream;…`), the busy and no-argument refusals, abort firing only
  `abort` and `loadend`, and the stream's read, close, error, pull, empty, cancel and tee behaviour.
  <br>Main-repo fix (the new `Polyfills/streams-and-file-reader.js` and `Features/StreamsBinding.cs`,
  plus `Features/BlobBinding.cs`, `Features/FetchBinding.cs` and the registration); regressions in
  `ReadableStreamTests` and `FileReaderTests`.
- **No DOM wrapper had a real prototype** — every one reported `constructor.name` of `"Object"`.
  `instanceof` already answered, because the interface globals carry an `@@hasInstance` hook that
  reads `nodeType`, which made the gap narrower than it looks and also more confusing:
  `node instanceof Text` was `true` while `node.constructor.name` was `"Object"` and
  `Object.getPrototypeOf(node) === Text.prototype` was `false`. Anything keyed on the constructor —
  debugging output, logging, dispatch — read the wrong thing.
  <br>**Fixed for the non-element wrappers**: `Text`, `Comment`, `DocumentFragment` and
  `DocumentType` are linked to their interface prototypes at the single choke point that mints every
  wrapper. `DocumentType` had no global and gained one. The gain is not only cosmetic — the
  prototype is genuinely in the chain, so extending `Text.prototype`, the ordinary polyfill idiom,
  now reaches instances where the assignment used to go to an object nothing inherited from.
  <br>**The boundary is deliberate and is asserted, not just described.** Each of these node kinds
  has exactly one interface fixed by its node type, so the mapping is a fact. An *element's* is a tag
  question over a table whose entries overlap and which omits tags a browser still names, so a guess
  would put a wrong name where `"Object"` is at least not misleading; `Attr` is not a canonical
  `DomNode` and so is not minted where the link is applied. A regression asserts both still report
  `"Object"`, so if either starts naming its interface the fixture is updated deliberately rather
  than the change landing unnoticed. Members also stay own properties, leaving the interface
  prototypes themselves empty — the larger object-model change, and what this item's remainder in
  [open](broiler-js-gaps-open.md#dom-interface-and-collection-model) now names precisely.
  <br>Main-repo `Broiler.HtmlBridge.Dom` fix (`DomBridge/WrapperPrototypes.cs`, applied from
  `DomBridge/JsObjects.cs`); regressions in `WrapperInterfacePrototypeTests`.
- **`document.fonts.check()` accepted malformed shorthands**, answering `true` for strings that are
  not fonts — `check('not-a-font')`, `check('12px')`, `check('px monospace')` — where every browser
  throws a `SyntaxError` (css-font-loading-3). `true` is the answer that does the damage: a page
  feature-testing a font it cannot have is told it has it. Only an absent or empty string threw.
  <br>**The deviation was deliberate**, and the reason given for it was risk: rejecting a shorthand
  Broiler merely failed to *parse* would break pages over a diagnostic it could not produce. The
  answer to that is to parse the shorthand properly rather than to accept everything. The `font`
  grammar is small and closed (CSS Fonts 4), so accepting exactly it is not an approximation —
  a system-font keyword, or an optional unordered run of style/variant/weight/stretch, then a
  font-size with an optional `/line-height`, then a family list.
  <br>**Over-rejection is the failure mode**, so the regression is a *table* and every row of it is a
  Chromium answer taken through Playwright — the 26 that must be accepted as much as the 18 that must
  be rejected. The accepted half deliberately carries the awkward spellings: `oblique 40deg`,
  `calc(1em + 2px)`, a glued `16px/2`, a quoted family, a bare `900` weight, runs of `normal`, and a
  multi-name family list. The tokenizer is quote- and paren-aware for exactly those, and an unquoted
  family name may not begin with a digit, which is what makes `12px 12px serif` a malformed family
  rather than a family called "12px serif".
  <br>What did **not** change is the modelling: a shorthand that parses still answers `true` and
  `load()` still resolves, because Broiler resolves fonts synchronously and no load is ever in
  flight. That half stays in [open](broiler-js-gaps-open.md#cssom-fonts-svg-and-js-visible-layout-algorithms)
  as a capability decision rather than a defect. Main-repo fix
  (`Broiler.HtmlBridge.Dom/Polyfills/content-rendering-polyfills.js`); regressions in
  `FontShorthandValidationTests`.
- **`template.content` was a snapshot, not the parser-owned fragment** (HTML §4.12.3). The fragment
  was built from a deep *copy* of children that stayed in the template's own child list, and the
  consequences went well past the two sides disagreeing.
  <br>A template's contents were **reachable from the document**: `t.querySelector('.row')` found
  them where a browser answers `null`, and `document.querySelector`/`getElementsByTagName` saw them
  too — so a page walking itself processed the markup it was meant to stamp later. And writing
  `t.innerHTML` rewrote the element's children while `content` kept the cached copy, so the ordinary
  way to build a template dynamically and then stamp it produced the **old** markup, with nothing to
  indicate the write had gone somewhere else.
  <br>**Fixed** by doing what the specification has the parser do: at the end of the parse every
  remaining `<template>`'s children are moved into its contents fragment and the element is left
  childless. Ordering matters — it runs *after* the declarative-shadow-root pass, because a
  `<template shadowrootmode>` is not inert and its children belong in the shadow tree rather than in
  a fragment. Two consumers follow the children: serialization reaches through to the fragment (a
  browser's `outerHTML` emits template contents for the same reason, and without it a template's
  markup would vanish from the serialized document), and `innerHTML` reads and writes the fragment.
  A template built by `createElement` is deliberately *not* diverted — only the parser diverts — so
  `t.appendChild(x)` appends to the element as it does in a browser while `t.innerHTML` still reaches
  the fragment.
  <br>The serializer needed no submodule change: it takes a bridge-supplied adapter, so which node
  list it walks was already the parent's to decide. Nothing rendered differently either way — a
  template's contents are inert, and `DeclarativeShadowDomTests` already pinned that.
  <br>Every expectation is a Chromium answer taken through Playwright. Main-repo
  `Broiler.HtmlBridge.Dom` fix (`DomBridge/Utilities.cs`, `DomBridge.HtmlParsing.cs`,
  `DomBridge.Serialization.cs`, `DomBridge/HtmlFragmentMutation.cs`); regressions in
  `TemplateContentTests`. One difference from the reference is left standing and recorded in
  [open](broiler-js-gaps-open.md#dom-interface-and-collection-model): the fragment's wrapper reports
  `constructor.name` of `"Object"`, which is the wrapper-prototype item, not this one.
- **Form association was entirely absent** — `control.form`, `control.labels`, `label.control` and
  `label.form` were all `undefined`. `control.form` is how a script reaches the form from a control
  it was handed (an event target, a query result), so the ordinary `input.form.submit()` threw on the
  property access rather than on the call; `control.labels` is how accessibility and validation code
  finds the text describing a field, and `labels.length` threw rather than answering zero. Recorded
  as open when the form default/reset family was closed and deliberately left for later, because
  `labels` is a live `NodeList` and that did not exist yet — the entry above is what unblocked it.
  <br>**Fixed:** the form owner resolves the `form` content attribute first (a control rendered
  outside the form it submits is the whole point of it), then the nearest ancestor `<form>`;
  `labels` is a live `NodeList` in tree order covering both spellings, `for` and wrapping;
  `label.control` honours `for` even when it names nothing, so a label wrapping one control while
  pointing at a missing id labels nothing rather than quietly labelling what it wraps.
  <br>**Two reference answers contradict the plausible reading**, which is why they were checked
  rather than reasoned about. `label.form` follows the label's *control*, not the label's own
  ancestry — a label outside every form whose `for` points into one reports that form, where
  treating the label as an ordinary form-associated element gives `null`. And **absence is specified
  three distinguishable ways**: a `<div>` has no `labels` property *at all*, an
  `<input type=hidden>` has one and it is `null`, and a labelable control with no labels has an
  empty list. Answering an empty list everywhere would have been wrong in two of the three — which
  is also why these members are installed per tag rather than on every element wrapper as the
  bridge's other form members are.
  <br>Main-repo `Broiler.HtmlBridge.Dom` fix (`Features/FormAssociationBinding.cs` and its host,
  installed from `DomBridge/ElementInterfaces.cs`); regressions in `FormAssociationTests`.
- **`NodeList` and `HTMLCollection` were plain JavaScript arrays**, which is wrong three ways — and
  the third one changed results rather than raising errors.
  <br>Neither interface was defined at all, so `instanceof` was a `ReferenceError` and
  `childNodes.constructor.name` answered `"Array"`. `item()` and `namedItem()` did not exist, while
  `map`, `filter` and `slice` did — the opposite of a browser in *both* directions, so a page
  feature-detecting either way branched wrongly. And an array is a **snapshot**, so the collections
  the specification defines as *live* were not: `var kids = el.childNodes; el.appendChild(x);
  kids.length` grows in a browser and did not here. That last one returns a wrong number rather than
  failing, which is how it sat under passing tests.
  <br>**Fixed** as real interfaces with real prototypes, and with each collection live or static as
  its own specification says: `childNodes` (DOM §4.4), `getElementsByTagName`/`ByClassName` (§4.5,
  §4.9) and `getElementsByName` (HTML §3.1.5) are live; `querySelectorAll` (§4.2.6) is static, the
  one the specification defines as a snapshot. A collection holds the *function* that produces its
  contents rather than the contents, so one type serves both and the difference sits at the call
  site. The prototype methods are plain JavaScript written against `this.length` and `this[i]`,
  which keeps `Symbol.iterator`, `entries`/`keys`/`values` and correct `this` handling out of C#,
  and puts them on the prototype where Web IDL wants them — `NodeList.prototype.item` is the
  function the instance uses.
  <br>This is track 6 **action 1**, "establish real interface prototypes and Web IDL collection
  behavior *before* adding more compatibility-only constructor globals", so these two deliberately
  are **not** the `@@hasInstance` shims the per-tag `HTML*Element` interfaces use: an instance's
  prototype really is `NodeList.prototype`. Element wrappers still answer through the hook, and that
  half stays [open](broiler-js-gaps-open.md#dom-interface-and-collection-model).
  <br>**Two mistakes are worth recording, because both looked like success.** Intercepting reads
  alone was not enough: an array index does not arrive as a string key, so overriding only that
  overload left `list[0]` answering `undefined` while `length`, `item()` and every method written
  against `this[i]` worked — a collection that reports the right count and iterates correctly and
  cannot be indexed. Fixing that exposed the deeper one: `Array.prototype.map.call(list, …)` read
  `length` correctly and produced a hole for every element, because an array generic asks whether an
  index is *present* before reading it, and an object with no own indexed properties says no — as do
  `Object.keys`, `for…in` and spread. Presence, enumeration and retrieval are separate entry points
  with no single hook, so the indices are **materialized** from the contents function on each read
  instead, and generic algorithms then work on a collection without knowing what it is. That is the
  shape the bridge's other live collection, the CSSOM `cssRules` list, already used.
  <br>**Every expectation is a Chromium answer** taken through Playwright, including the three-way
  liveness split, which is the assertion easiest to get subtly wrong. Main-repo
  `Broiler.HtmlBridge.Dom` fix (`Features/DomCollectionBinding.cs` and the collection call sites in
  `Features/NodeAccessorsBinding.cs`, `Features/SelectorsBinding.cs`,
  `Features/DocumentQueryBinding.cs` and `DomBridge/Utilities.cs`); regressions in
  `DomCollectionInterfaceTests`.
- **Form-control default, reset and radio-group semantics** — the retest-queue entry that was carried
  as *uncharacterized*. Characterizing it split the entry in two: **the dirty half was already
  correct**, and the default and reset halves were absent outright.
  <br>Correct, and now pinned so it stays that way: a property write does not reflect to the content
  attribute, once the dirty value flag is set a later `setAttribute('value', …)` no longer moves the
  value, dirty checkedness decouples from the `checked` attribute the same way, a `<select>` takes its
  initial selection from the markup's `selected`, and setting `checked` through the property unchecks
  the rest of that radio group and leaves other groups alone.
  <br>**Absent, and fixed:** `input.defaultValue`, `textarea.defaultValue` and `input.defaultChecked`
  were `undefined`, so a page comparing the current value against the original to decide whether a
  field is unsaved compared against `undefined` and concluded "changed" for every field, including
  ones it had just reset. `form.reset()` was `undefined`, so the call a "clear this form" control is
  written as was a TypeError that aborted the handler rather than clearing anything. An untouched
  `<textarea>` reported `""` rather than its child text, so a form read before the user typed
  anything submitted an empty field. `option.defaultSelected` read the bridge's runtime slot alone
  and was `false` for every option the markup had selected — including the one the select was
  showing. And an already-checked radio *inserted* into a group left two members checked: the
  property setter's exclusivity walk never runs for it, because it was checked while still detached
  and in a group of one.
  <br>The reset itself is small because the state model was already right: a reset is defined over
  the dirty flags, and the bridge's per-element `FormControl` runtime slots *are* those flags, with
  "unset" already meaning "tracks the markup" everywhere the IDL getters fall back through. So the
  algorithm removes slots rather than computing replacement values. Fixing the textarea default
  exposed one coupling: writing `textarea.value` had been storing a `value` content attribute that
  nothing reads on a textarea — harmless while the getter read that same attribute back, a lost write
  once the getter started falling back to the child text — so the setter now sets the dirty flag, as
  HTML §4.10.11 specifies and as `<input>` already did.
  <br>**Every expectation was taken from Chromium, not from a reading of the specification**, through
  Playwright against the pinned browser. Two would plausibly have been got backwards otherwise:
  whether appending an already-checked radio re-imposes exclusivity at all (it does), and which
  member of a group with two `checked` attributes survives a reset (the last in tree order, because
  the rule fires whenever a radio becomes checked, so restoring them in order leaves each unchecking
  the ones before it).
  <br>Main-repo `Broiler.HtmlBridge.Dom` fix (`DomBridge/FormReset.cs`, `Features/FormBinding.cs`,
  `Features/FormControlBinding.cs`, `DomBridge.SelectHost.cs`, and the insertion hook in
  `DomBridge/HtmlFragmentMutation.cs` — the one choke point every insertion path already reaches);
  regressions in `FormDefaultsAndResetTests`. Still absent and *not* part of this entry:
  `input.form` and `input.labels`, which are the form-association surface rather than the
  dirty/default/reset family, recorded in
  [open](broiler-js-gaps-open.md#dom-interface-and-collection-model).
- **A linked stylesheet's rules reached neither `cssRules` nor `getComputedStyle`, and the sheet
  reported no `href`.** A `<link rel="stylesheet">` appeared in `document.styleSheets` as a sheet
  with zero rules and a null location, and the elements it styled computed as if it were not there —
  measured against an inline `<style>` control carrying identical CSS, which answered `cssRules
  .length` 2, `display: flex`, `color: rgb(1, 2, 3)`, `marginTop: 7px` where the link answered 0,
  `inline`, `rgb(0, 0, 0)`, `0px`. It is what made the retired *"`getComputedStyle().display`
  reports `inline` for every element"* claim true after all for linked sheets.
  <br>**The open question is answered: it was not a `file:`-scheme defect.** `file:` and `http(s):`
  behaved *identically* — both failed for a relative `href` and both worked for an absolute one —
  which is what identified the cause. `GetStyleElementSourceText` handed the **raw `href` content
  attribute** to the resource loader, and the loader takes absolute URLs only
  (`ResourceLoader.LoadTextDirect` opens with a `UriKind.Absolute` guard and returns `null` for
  anything else). So the fetch was never issued for a relative href — the ordinary case — on any
  scheme. The whole collect → prefetch → fetch pipeline was present and correct, as the earlier
  characterization suspected; only the URL passed through it was.
  <br>It was invisible as a rendering bug because `HtmlRender` resolves and applies the link itself:
  paint and the CSSOM held two different stylesheet sets and only paint had the linked one, which is
  why the green-pixel assertion in `StylesheetBaseHrefTests` passed throughout.
  <br>**Fixed:** the href is resolved through `ResolveStyleSheetLinkUrl` before it reaches the
  loader. The base it resolves against is the **document** base URL — the first `<base href>`
  resolved against the page URL when the document declares one, the page URL otherwise — because a
  `<base href>` relocates a linked sheet (HTML §4.2.3) and the render-bound `RewriteLinkStyleSheetHrefs`
  pass already honours it; resolving against the page URL here would have read a *different* sheet
  than the one that paints. `data:` hrefs still bypass the loader through the same seam as before.
  Both prefetch sites move with the consuming path — `PrefetchExternalStylesheets` and the
  speculative preload scan (`ResolvedUrls` rather than `RawUrls`) — since a prefetch keyed on a
  differently-normalized URL is never consumed and would double the requests instead of overlapping
  them. `CSSStyleSheet.href` was a hardcoded `null` for every sheet; it is now a live getter
  reporting the same resolved location for a linked sheet and `null` for an inline `<style>`, per
  CSSOM §2.1. Main-repo `Broiler.HtmlBridge.Dom` fix (`DomBridge/Css.cs`,
  `DomBridge/StyleSheets.cs`, `DomBridge.PreloadScan.cs`); regressions in
  `LinkedStylesheetCssomTests`, which pin relative and absolute hrefs over both `file:` and a local
  `http:` origin, and `<base href>` relocation, each against the inline control rather than against
  transcribed values.
- The **document-level** `removeChild`/`insertBefore` had the same defect on their own code path
  (`NodeMutationBinding`, reached by `document.removeChild(…)` / `document.insertBefore(…)`, distinct
  from the element methods above). `document.removeChild` returned the node unchanged — what a
  successful call returns — while mutating nothing. `document.insertBefore` was the worst shape in
  the whole family: given a reference node that was *not* a child it fell through to **append**, so
  the node was silently mutated into a position the caller never asked for, landing at the end of the
  document instead of before the reference. **Fixed:** both raise `NotFoundError` (`code` 8) from the
  pre-mutation validation point. The `insertBefore(node, null)` append is untouched and explicitly
  pinned by a round-trip regression — a null reference *means* append per DOM §4.2.3, so the new
  throw had to not swallow it. Main-repo `Broiler.HtmlBridge.Dom` fix
  (`Features/NodeMutationBinding.cs`), landed directly rather than as a submodule patch; regressions
  in `DocumentMutationExceptionTests`. What remains of this family — `setAttribute` name validation
  and `querySelector` selector validation — sits behind the `Broiler.DOM` and `Broiler.CSS`
  submodules and stays in [open](broiler-js-gaps-open.md#dom-interface-and-collection-model).
- The element tree-mutation methods did not raise `NotFoundError` for a node that is not a child
  (DOM §4.2.3, which puts the same rule in the pre-insert, pre-remove and replace steps).
  `insertBefore` threw a plain error whose message merely *began* with the name — so
  `e instanceof DOMException` was false, `e.name` was `"Error"`, `e.code` was `0` — while
  `removeChild` and `replaceChild` did something worse: they **returned the value a successful call
  returns** (the removed or replaced node) and mutated nothing. The caller was told the mutation had
  happened. Code that removes a node and then re-parents the returned value silently operated on a
  node still attached to its original parent, with no error anywhere to trace it to. **Fixed:** all
  three raise a real `NotFoundError` `DOMException` (`code` 8) naming the method, from the validation
  point *before* any mutation, which is where the specification puts it. (`replaceChild`'s second,
  defensive index re-check runs after the new node has already been detached, so it still returns
  rather than throwing out of a half-finished mutation.) Wiring rather than a new mechanism: the
  `HierarchyRequestError` circular-reference guard beside these very call sites was already minting
  correct exceptions through `DomBridge.ThrowDOMException`, and only the not-found branches were not
  reaching it. Every successful mutation is unchanged — including `removeChild`'s return value and
  the `insertBefore(node, null)` append form — and a `DOMException` is still an `Error`, so existing
  message-based handling is unaffected. Main-repo `Broiler.HtmlBridge.Dom` fix
  (`Features/TreeMutationBinding.cs`), landed directly rather than as a submodule patch; regressions
  in `TreeMutationExceptionTests`. The document-level equivalents (`NodeMutationBinding`) and
  `setAttribute`/`querySelector` name and selector validation remain in
  [open](broiler-js-gaps-open.md#dom-interface-and-collection-model).
- CharacterData failures were not proper `DOMException` objects. All four mutation methods —
  `substringData`, `insertData`, `deleteData`, `replaceData` — plus `Text.splitText` threw a plain
  error for an out-of-range offset, and the message was the string `"INDEX_SIZE_ERR"`: the legacy
  constant's *name*, used as prose. Nothing a page can branch on came out of that.
  `e instanceof DOMException` was false, `e.name` was `"Error"` and `e.code` was `0`, so both checks
  a caller actually writes failed, and a specified, recoverable condition read as an internal fault.
  **Fixed:** all five raise a real `IndexSizeError` `DOMException` (`code` 1) carrying a message that
  names the method and the offending offset — DOM §4.10 opens each of these methods with "If offset
  is greater than length, throw an IndexSizeError DOMException", and §4.11 gives `splitText` the same
  rule. This is wiring rather than a new mechanism: the bridge's `ThrowDOMException` helper already
  minted correct exceptions for `appendChild`'s `HierarchyRequestError` and `createElement`'s
  `InvalidCharacterError`, and the CharacterData methods simply were not reaching it — they had no
  `JSContext` to mint one in, so the binding's host contract now exposes it exactly as
  `INodeMutationHost` already did. A `DOMException` is still an `Error`, so existing
  `catch (e) { e.message }` handling is unaffected, and every in-range operation is unchanged
  (including the `offset == length` boundary, which is in range and returns the empty string).
  Main-repo `Broiler.HtmlBridge.Dom` fix (`Features/CharacterDataBinding.cs`,
  `Features/ICharacterDataHost.cs` and its bridge implementation), landed directly rather than as a
  submodule patch; regressions in `CharacterDataExceptionTests`. The same sweep found the tree-mutation
  half of this family still open — `insertBefore`/`removeChild`/`setAttribute`/`querySelector` — which
  is recorded in [open](broiler-js-gaps-open.md#dom-interface-and-collection-model).
- The six `Node.DOCUMENT_POSITION_*` constants were `undefined` everywhere — on the `Node` global,
  on its prototype, and on every node instance — while the node-type constants beside them were
  defined. `compareDocumentPosition` returned a correct DOM §4.4 bitmask (this was checked and is
  recorded under *Retired* below), but with the names absent a page could not decode it:
  `result & Node.DOCUMENT_POSITION_CONTAINED_BY` is `result & undefined`, which evaluates to `0`
  rather than throwing — so a containment test did not fail loudly, it silently answered "not
  contained" for **every** pair of nodes. A right answer that cannot be read is the failure mode
  here, which is why the bitmask being correct was not enough to close the item.
  **Fixed:** all six position bits are now installed with their specified values (`0x01`…`0x20`)
  on the `Node` global and prototype and on every node object.
  <br>The type constants were installed from five hand-copied blocks that had drifted to different
  subsets — the element and non-element wrappers carried eight of the twelve, the document and
  sub-document only six (no `ATTRIBUTE_NODE`, no `CDATA_SECTION_NODE`) — and none carried the
  position bits, so the omission was identical everywhere by construction and a sixth copy would
  have inherited it. The five blocks are replaced by one `NodeConstantsBinding.Install` installer
  carrying the complete interface: twelve type values (adding the genuinely-missing
  `PROCESSING_INSTRUCTION_NODE`, plus the legacy `ENTITY_REFERENCE_NODE`/`ENTITY_NODE`/
  `NOTATION_NODE` the `Node` global polyfill already listed, so an instance and the global agree)
  and the six position bits. Net effect on the touched files is 20 fewer lines.
  Main-repo `Broiler.HtmlBridge.Dom` fix (`Features/NodeConstantsBinding.cs` and its five call
  sites, plus the `Node` polyfill in `DomBridge/Utilities.NameValidation.cs`), landed directly
  rather than as a submodule patch; regressions in `NodeConstantsTests`.
- **The `:is()` aliases matched every element. Now live.** `:matches()`, `:any()`,
  `:-webkit-any()` and `:-moz-any()` are the historical spellings of `:is()`; all four sat in
  `Broiler.CSS.Dom`'s `CssSelectorMatcher` recognized-but-unmodelled set, fell through its lenient
  default arm, and matched **every** element. The cascade reaches the same matcher, so
  `:-webkit-any(h1) { color: red }` painted the whole page — a rendering bug rather than only a
  `querySelector` one. It shipped as a `patches/` file because the submodule remote is outside this
  session's GitHub scope; the maintainer has since applied it, and the pinned `Broiler.CSS` pointer
  carries *Stop the `:is()` aliases matching every element*, so it is live and the patch file is
  gone. Measured against Chromium: only the `-webkit-` spelling is still accepted and it behaves
  exactly like `:is()`, while the other three were removed from the platform and match nothing —
  Broiler now agrees on all four, and the cascade paints only the `h1`.
  <br>What stays lenient, deliberately, is an unknown **vendor-prefixed** functional pseudo-class,
  which still matches everything (`:-webkit-frob(p)` answers `<html>`); an unknown *unprefixed* one
  matches nothing, as it should. `DomApiSyntaxTests` pinned the pre-patch over-matching and was
  re-taken from Chromium: `The_Is_Aliases_Match_What_A_Browser_Matches` holds the fix and
  `An_Unknown_Vendor_Prefixed_Functional_Pseudo_Class_Still_Over_Matches` holds the residual policy.
- **`Range` was not an interface, was missing five operations, and rejected nothing.** Three gaps in
  one surface, all of them script-visible.
  <br>**No interface.** `Range` did not exist as a global, so `typeof Range` was `"undefined"` and
  both `new Range()` and `r instanceof Range` were `ReferenceError`s — the kind that aborts the whole
  script, not just the line that asked. `document.createRange()` handed back a plain object whose
  `constructor.name` was `"Object"`, whose `Object.prototype.toString` was `[object Object]`, and
  whose 29 members were its own properties, so `Range.prototype.setStart` had nothing to be.
  <br>**Five missing operations:** `comparePoint`, `isPointInRange`, `intersectsNode`,
  `createContextualFragment` and `detach` were all absent.
  <br>**No argument checking.** An offset past the container's length was clamped into it rather than
  raising `IndexSizeError`, so a range silently pointed somewhere else and the wrongness surfaced
  later as a wrong extraction; a missing or non-`Node` argument returned `undefined`;
  `selectNode` on a parentless node was a no-op; `selectNodeContents(doctype)` escaped as a bare
  `Error` carrying a .NET stack trace; `compareBoundaryPoints` answered `0` both for an unknown
  comparison method and for a source range in another tree — indistinguishable from a legitimate
  "equal"; and `insertNode` would put a doctype inside a paragraph.
  <br>**Fixed** by registering `AbstractRange` and `Range` as real interfaces and moving every member
  onto their prototypes. A range's boundaries live in a weak table keyed by the range object, so a
  prototype method finds its own state from its receiver — which is what leaves the instance with no
  own properties at all (`Object.getOwnPropertyNames(r)` is `[]`, as in a browser) and makes an
  illegal invocation a `TypeError` rather than a wrong answer. **This is the first DOM interface here
  whose members really are on its prototype**; the rest still install theirs per wrapper, which is
  the open half of the wrapper item in [open](broiler-js-gaps-open.md#dom-interface-and-collection-model).
  <br>**The boundary getters went on `AbstractRange`, not on `Range`** — measured, not assumed: a
  browser's `Range.prototype` genuinely does not own `startContainer`, `startOffset`,
  `endContainer`, `endOffset` or `collapsed`, because they are the base interface's.
  <br>**Two answers reasoning gets wrong**, both taken from the probe corpus rather than the
  grammar. `setStart(node, -1)` is an `IndexSizeError` and not a `TypeError`, because Web IDL
  converts `-1` to `4294967295` first and it is then merely too large; and by the same conversion
  `compareBoundaryPoints(3.7, r)` is *accepted* (truncating to `END_TO_START`) while `4` is a
  `NotSupportedError`.
  <br>**The measurement also found a live bug beside the ones it was looking for:**
  `START_TO_END` and `END_TO_START` were swapped. DOM §4.5 makes `START_TO_END` compare *this*
  range's end against the source's start and `END_TO_START` this range's start against the source's
  end; the bridge had each reading the other pair, so two of the four comparisons answered the wrong
  sign. Three `DomTraversalAndRangeTests` cases pinned the swapped answers and were re-taken from
  Chromium. `cloneRange` was minting its copy against the main document whatever the original's root
  was, which is fixed in the same edit.
  <br>Main-repo `Broiler.HtmlBridge.Dom` fix (`Features/TraversalBinding.RangeInterface.cs`,
  `Features/TraversalBinding.Range.cs`, `Features/TraversalBinding.cs`,
  `DomBridge/Registration/Polyfills.cs`); regressions in `RangeInterfaceTests`, with the corrected
  comparison cases in `DomTraversalAndRangeTests`. `StaticRange` — the other `AbstractRange`
  subclass — and `window.getSelection` remain absent and are recorded in
  [open](broiler-js-gaps-open.md#dom-interface-and-collection-model).

- **`Blob`, `File` and `FileList` were undefined, and `response.blob()` handed back a look-alike.**
  The open note deferred these for a decision, on the grounds that `FileList` is only reachable
  through a file selection this engine does not have. That reason cut the other way once measured: a
  browser reports an **empty** `FileList` for an input nobody has touched, so there is a correct
  answer to give and `undefined` was not it — `input.files` read `undefined` on a file input *and* on
  a text input, where a browser gives a list and `null` respectively, so the standard guard
  `if (input.files && input.files.length)` was a `TypeError` on the very input it is written for.
  The decision is therefore **implement**, and it is done.
  <br>`Blob` is reached by ordinary pages and not only by upload code: it is how a page builds a
  downloadable payload (`URL.createObjectURL(new Blob([csv], {type: 'text/csv'}))`), how it posts
  binary through `fetch`, and what `response.blob()` is supposed to return. The bare name was a
  `ReferenceError`, which aborts the script rather than the statement.
  <br>**It also replaced a shape-only stub, which is the part worth naming.** `response.blob()`
  already answered — with a plain object carrying `size`, `type`, `text()` and `arrayBuffer()` and
  nothing else, so `constructor.name` was `"Object"`, there was no `slice`, and
  `(await response.blob()) instanceof Blob` could not even be asked. That stub was invisible
  precisely because the interface it imitated did not exist to be compared against; it now mints a
  real one.
  <br>**Fixed** on the same weak-table/prototype machinery `Range` and `Selection` use, so an
  instance has no own properties and an illegal invocation is a `TypeError`. `File` really extends
  `Blob` through the prototype chain rather than through a hook. `FileList` joined the existing
  indexed-collection machinery rather than getting a second one, so it is live over its contents and
  needs no new shape the day a file selection exists.
  <br>**Three answers are measured rather than reasoned, and each is a trap.** The parts argument is
  a Web IDL `sequence`, which deliberately does not accept a string however iterable a string is — so
  `new Blob('abc')` is a `TypeError` and not a three-byte blob. A `type` carrying a character outside
  U+0020–U+007E is discarded **entirely** rather than kept or escaped. And `slice` gives its result an
  *empty* type rather than inheriting the source's, so the obvious `blob.slice(0, n)` loses the
  content type unless it is passed again.
  <br>**`Blob.prototype.stream()` is deliberately absent** rather than stubbed: it returns a
  `ReadableStream`, and this engine already carries one partial stream — the object `response.body`
  hands back — which a second copy should not be written against. Whether to build a real
  `ReadableStream` is its own capability decision; until it is made a page feature-detecting
  `blob.stream` takes its `arrayBuffer()` fallback, which works. `FileList.prototype` carries `item`
  but not `length`, where a browser has both — that is the shared collection machinery answering
  `length` from the host rather than a prototype accessor, identical for `NodeList`, and not this
  interface's to change. Both are pinned.
  <br>Main-repo `Broiler.HtmlBridge.Dom` fix (`Features/BlobBinding.cs`,
  `Features/DomCollectionBinding.cs`, `Features/FormControlBinding.cs`, `Features/FetchBinding.cs`,
  `DomBridge/Registration/Polyfills.cs`); regressions in `BlobInterfaceTests`. `FileReader` remains
  absent and is recorded in [open](broiler-js-gaps-open.md#dom-interface-and-collection-model).
- **`window.getSelection()` did not exist, and neither did `Selection` or `StaticRange`.** The
  follow-up the `Range` work recorded beside itself. All three were absent, so `window.getSelection`
  read `undefined` and the bare `Selection`/`StaticRange` were `ReferenceError`s — the kind that
  aborts the script rather than the statement. That reaches ordinary pages and not only editors: the
  copy-to-clipboard idiom every page shares is `sel.removeAllRanges(); sel.addRange(range)`, and
  `window.getSelection().toString()` is how a page reads what was picked.
  <br>**The open note said this was "a selection *model* rather than a missing name, since this
  engine has no user selection to report", and that turned out to be half right.** There is no user
  selection — but that is precisely the state a browser is in on a freshly loaded page: `rangeCount`
  `0`, `type` `"None"`, `anchorNode` `null`. Everything a script then does to it — `addRange`,
  `collapse`, `extend`, `setBaseAndExtent`, `selectAllChildren`, `deleteFromDocument` — has an answer
  that does not depend on a user, and that scripted half is what is implemented. **Fixed** on the
  prototype machinery the `Range` work built: `Selection` is a real interface with its members on its
  prototype and its state in a weak table, and `StaticRange` shares `AbstractRange`'s five getters
  with `Range` by holding four captured values where a range holds a live one.
  <br>**Three details are measured rather than reasoned, and two contradict the specification's
  wording.** A node or range belonging to another tree is silently **ignored** by `addRange`,
  `collapse`, `selectAllChildren` and `setBaseAndExtent` rather than rejected — and "another tree"
  includes a *detached* one, so collapsing into an element a page has built but not yet inserted does
  nothing at all. Yet an out-of-range offset or a doctype in that same argument still throws, so the
  argument is validated *before* the tree is consulted. And a selection carries exactly one range: a
  second `addRange` is dropped, not added.
  <br>The selection holds the page's **own** range object, so `sel.getRangeAt(0) === r` and a later
  edit of that range moves the selection — which is what a browser means by the selection *being* the
  range rather than a copy of it. `extend` is the one operation a bare `Range` cannot express, since
  the focus may end up before the anchor; the range still runs low-to-high and the selection
  remembers the direction.
  <br>A document with no browsing context answers `null`, which is the browser's answer: a frame's
  `contentDocument`/`contentWindow` gets its own `Selection`, a `createHTMLDocument` result gets
  `null`. The bridge already knew the difference — `BrowsingContextManager` links exactly the
  displayed documents — so the line is drawn from real state rather than guessed.
  <br>**Two operations are deliberately absent rather than stubbed:** `modify()`, which moves the
  selection by character/word/line and so needs the text-segmentation model this engine does not
  have, and `getComposedRanges()`, which is shadow-tree composition. A page feature-detecting either
  takes its fallback, where a stub would claim a movement that silently does nothing. They are the
  only two of Chromium's 30 `Selection.prototype` members not present, and both are pinned.
  <br>Main-repo `Broiler.HtmlBridge.Dom` fix (`Features/TraversalBinding.Selection.cs`,
  `Features/TraversalBinding.RangeInterface.cs`, `Features/TraversalBinding.cs`,
  `Features/SubDocumentBinding.cs`, `Features/SubWindowBinding.cs`); regressions in
  `SelectionInterfaceTests`. What stays absent, and is not part of this gate, is the half that needs
  a user: nothing populates the selection on its own, no `selectionchange` fires from input, and the
  selection is not painted — see [open](broiler-js-gaps-open.md#dom-interface-and-collection-model).
- **`innerHTML` dropped every text and comment child.** Found while measuring `Range`, from a probe
  that used `innerHTML` to read back what `insertNode` had done and got an answer with the text
  missing. The read side filtered its child list with `.OfType<DomElement>()`, so
  `<div>ab<b>c</b>d<!--k--></div>` read back as `"<b>c</b>"` and a `<div>` holding nothing but text
  read back as `""` — while that same element's `textContent`, `childNodes` and `outerHTML` all
  reported the text correctly. It returns a *wrong value* rather than failing, which is what let it
  sit unnoticed under passing tests.
  <br>The filter is a leftover from the facade era, when a text child was not a node at all but a
  string on its parent's element record; construction has produced canonical `DomText`/`DomComment`
  children since. `outerHTML` never had the bug because it hands the whole subtree to the serializer
  in one call instead of re-serializing children one at a time — so the two accessors disagreed
  about the same tree, which is the sharpest statement of it. The document-level `SerializeToHtml`
  is the whole-subtree call too, which is why rendering and the render tests never saw it.
  <br>**Fixed** by serializing every child through the same adapter, which already handles text,
  comments, doctypes and fragments — including the raw-text rule, so a `<script>`'s content stays
  literal while ordinary text is escaped. Main-repo `Broiler.HtmlBridge.Dom` fix
  (`DomBridge.Serialization.cs`); regressions in `InnerHtmlChildSerializationTests`.

## Retired — did not reproduce

Each was checked against the current pointer; the cases tried are recorded so the note is not
carried again as an asserted gap.

### Track 5 — URL parsing

- Non-special URLs such as `data:` can report an empty `.protocol`. **Does not reproduce.** Checked
  through a DomBridge-attached capture: `new URL('data:text/plain,hi').protocol` → `"data:"`,
  `new URL('blob:https://x/y').protocol` → `"blob:"`, `new URL('mailto:a@b.c').protocol` →
  `"mailto:"`, and the special-scheme control `new URL('https://a.b/c').protocol` → `"https:"`. All
  four carry the trailing colon the URL Standard requires. Narrowed rather than retained: if a
  specific non-special scheme still answers empty, it needs naming, because the general claim is not
  current.

### Track 6 — DOM and CSSOM

- Qualified mixed-case attributes such as `viewBox`, `preserveAspectRatio` and `xlink:href` can be
  inaccessible through canonical DOM lookup. **Does not reproduce.** Checked through a
  DomBridge-attached capture over inline SVG carrying all three:
  `svg.getAttribute('viewBox')` → `"0 0 10 10"`, `svg.getAttribute('preserveAspectRatio')` →
  `"xMidYMid"`, and `a.getAttribute('xlink:href')` → `"#z"`. Each returned the authored value under
  its authored spelling. Narrowed rather than retained: if a specific qualified name is still
  unreachable it needs naming, because the general claim is not current.
- `getComputedStyle().display` can report `inline` for every element. **Retired too broadly — see the
  correction below.** The original check used an **inline `<style>` block**, where
  `getComputedStyle` reported `block` and `flex` correctly, and that much stands: the computed value
  does track the cascade for inline sheets. But the claim was never only about inline sheets, and a
  later check with a **linked** stylesheet reproduced it. The accurate statement is the open entry
  *A linked stylesheet's rules reach neither `cssRules` nor `getComputedStyle`*, which now carries
  the evidence; this retirement should be read as covering the inline case only.

- `compareDocumentPosition` returns `-1`, `0`, or `1` instead of the required position bitmask.
  **Does not reproduce.** Checked through a DomBridge-attached capture over a document with a
  containing `div#a > span#b` and a following `p#c`: `a.compareDocumentPosition(b)` → `20`
  (`DOCUMENT_POSITION_CONTAINED_BY` 16 | `DOCUMENT_POSITION_FOLLOWING` 4),
  `a.compareDocumentPosition(c)` → `4`, `c.compareDocumentPosition(a)` → `2`
  (`DOCUMENT_POSITION_PRECEDING`), and `a.compareDocumentPosition(a)` → `0`. Those are the DOM §4.4
  bitmask values, not a `-1`/`0`/`1` comparator. The genuine remaining half — the
  `Node.DOCUMENT_POSITION_*` constants being `undefined`, so the returned bits could not be named —
  was found by the same check and has since been **fixed**; see the Track 6 entry above.

### Track 1 — language and built-ins

- `new.target` is rejected in a direct eval nested inside an eval-compiled function.
  **Does not reproduce**; `staging/sm/class/newTargetEval.js` passes. It is in the failure
  manifest from an older run — confirm against a current CI run before removing the path.
- Comment and regular-expression-literal lexical edge cases, labeled and unlabeled `continue`,
  and block-scoped loop bindings. **Do not reproduce:** `test/language/comments`,
  `test/language/asi` and the `for`/`if`/`while`/`do-while`/`labeled` directories pass apart
  from the early-error cluster above.
- `slice`, `unshift`, `toReversed`, `reduceRight`, array mutation limits, near-maximum lengths,
  and Proxy-created results retain confirmed failure paths. **Largely does not reproduce:** all
  four directories pass except `slice/create-proto-from-ctor-realm-array.js`, a cross-realm
  species case.

  The one remaining case is [open](broiler-js-gaps-open.md#track-1--core-language-and-built-ins).

### From the retest queue

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
- ~~the archived observation that async continuations did not run under in-process `Eval` or
  `Execute`~~ — **retired: does not reproduce**, pinned by `AsyncContinuationDrainTests`. A job
  queued by script during an in-process `Eval` — a promise reaction, a chain of them, a
  rejection handler, or an async function's resumption after one or several `await`s — has run
  by the time the call returns, so a later `Eval` on the same context observes its effect; the
  same holds through `EvalWithTopLevelAwaitAsync`. Both halves of the contract are pinned: the
  reaction is not run inline during the script that queues it, and it is not lost either;
- ~~a rejected function-`prototype` write historically changing later `[[Construct]]`
  behavior~~ — **reproduced and fixed**; see
  [Track 1 — Objects, arrays, symbols](#track-1--objects-arrays-symbols-and-proxy-sensitive-behavior)
  above.

Sources:

- [TypedArray gate](../Broiler.JS/docs/roadmap/Component.md#immediate-correctness-gate-typedarrayprototypeset)
- [Older compliance triage](../Broiler.JS/docs/compliance/known-gaps.md)
- [M0 Test262 subset](../tests/m0-baseline/conformance/test262-subset/test262-subset-summary.md)
- [Historical status reconciliation](../Broiler.JS/docs/roadmap/Roadmap.status.md)
- [Archived async observation](../Broiler.JS/docs/roadmap/Archive.md)
- [Module initialization record](../Broiler.JS/docs/roadmap/Phase-1.status.md)
- [DOM form roadmap](../Broiler.DOM/docs/roadmap.md)
