# Pending submodule patches

Fixes that belong in a submodule (`Broiler.HTML`, `Broiler.CSS`, `Broiler.DOM`,
`Broiler.JS`, `Broiler.Graphics`) but could not be pushed to their remote from
the session that wrote them: the git proxy only authorises repos in the session's
GitHub scope, so a push to a submodule remote outside it returns **403**. Rather
than bump a submodule pointer at a commit CI cannot clone, the change is captured
here as a `git format-patch` file for a maintainer to apply.

## Applying

```sh
cd <Submodule>
git am --keep-cr ../patches/NNNN-<slug>.patch
git push origin HEAD
cd ..
git add <Submodule>        # bump the pointer only after the push succeeds
```

Delete the patch file and its row below once the pointer is bumped.

**`--keep-cr` is not optional for a patch that touches a file with CRLF line endings**, which
several `Broiler.JS` sources are (mixed CRLF and LF, within one file). `git am` runs the patch
through `mailinfo`, which normalizes the line endings of the diff body unless told not to — so the
context lines stop matching the file and the apply fails with *"patch does not apply"* on a patch
that is perfectly good. `git apply` does not have the problem, which is exactly why it is not the
check: these instructions use `am`, so `am` is what a patch has to survive. Verified per patch by
applying it to a clean checkout of the pinned pointer and diffing the result against the branch it
was generated from.

## Index

| Patch | Submodule | Note |
| --- | --- | --- |
| `0083-js-arithmetic-operand-census` | `Broiler.JS` | Item 3-1's first measurement, and it re-specifies the item off storage. Phase 3's ordering rests on the diagnosis that the boxes are minted by the **operators**; the half nobody had counted is how often a generic operator's two operands are *already* Numbers, which is what decides whether any fast path can be fed. `ArithmeticOperandDiagnostics` counts it on each generic arithmetic and bitwise operator (plus the `JSNumber` and `JSString` overrides, which a Number or String receiver reaches instead of the base): **73 817 515 of 73 818 646 invocations across the corpus arrive with both operands already Numbers — every one but 1 131 — and that population is 86.6% of all 85 248 348 boxes**. The guard a speculating native form needs is a branch that predicts perfectly. **Against it, the compiler's own `both are native` proof reaches 556 053, 0.75%** — and even that counts `+` alone, the only operator with a `JSValue × double` overload. *Compile-time provability reaches 0.75% of the arithmetic; run-time truth reaches 100.00% of it*, and six correct, invisible phase-3 items sit in that gap. **The consequence corrects item 3-1's own re-specification**: "storage plus an unboxed element read" assumes the problem is where the value is kept, and it is not — the operator already receives two Numbers whatever they are stored in; what it cannot do is *hand one back*, because its consumer is a `JSValue`. The shared half is a **run-time-guarded specialization of an arithmetic expression tree**, boxing only the root. It also partly reverses **3-8's "do not start as written"** without contradicting it: 3-8 priced that guard at the **local** (0.36% of boxing, correct), this counts it at the **operator** (86.6%). One smaller correction: a numeric literal is **already** a native double, so the second box in `a[0] * 1.5` is the compiler re-boxing a raw double to meet a `JSValue` operator, not `VisitLiteral`. **The counter's first version read zero on all seven suites** — the enable went next to the wrong one of two identical `NumberBoxingDiagnostics.Reset()` pairs — which is what `ArithmeticOperandCensusTests` exists to stop: 5 fixtures, 3 of which make the counter *discriminate* rather than merely fire. Counters off by default, no behaviour change, full repository suite **7 930 tests, 0 failures**. **Independent of `0082`** — no file in common — but generated against the same pin, so apply in number order. |
| `0082-js-relay-rewrite-once` | `Broiler.JS` | Item 1-1's remaining half — **measured before being built, and the measurement found a repeat inside the half that already landed.** Two new instruments. **`--compile-phases`** takes the parse / expression-tree / emission split on the *real* corpora rather than on `--compile-scaling`'s declaration walls, against the body-free control and with the closure rewrite as its own column: **parse 9.4–13.5%, tree construction 33.6–63.9%, emission 25–57%**, tree construction the largest single phase on five of six, and the parse — the part an early-error rule forbids deferring — a tenth of the whole. **`--defer-population`** counts what the ceiling table never could, by reading item 1-1's own registration and forcing counters after evaluating each corpus the way its harness does: **84–99.7% of a script's functions are never invoked** (jQuery 347 of 415, Mandreel 2 689 of 2 697). So the remaining half is over half the compile across a population that is almost entirely never needed — **and it also corrects this item's own ceiling table**, because `--compile-profile` stubs *outermost* bodies and jQuery has exactly one, the IIFE the library is written inside (99.91% of its bytes), which CodeLoad calls first. The build: `LambdaRewriter.Rewrite` descends through nested lambdas — that is how `CheckForClosure` threads a capture up the whole chain — and `RuntimeMethodBuilder.Relay` called it **again** per relayed site, so a lambda at depth *d* was walked *d+1* times and jQuery's whole tree was walked twice by a compile that emits almost nothing. Counted, every relay in a real compile is a repeat (**0 rewrites needed against 415, 978 and 1 574 skips**) and the repeat creates **0 captures the first walk had not** — the second counter, which is what makes the skip a removal of *repeated work* rather than of work. `Relay` now skips a lambda a descending walk has already entered, marked on the lambda; `RewriteRootOnly` — the async pre-rewrite, which stops at each nested lambda by design — marks nothing, so async and generator state machines are rewritten at relay exactly as before. Worth **0.782× on jQuery's whole compile and 0.867× on Typescript's, six of six ABBA pairs each**; **Box2D does not separate on that instrument** and its control arm's own spread is 55.6%, so the changed phase was measured directly — **emission 99.9 → 54.8 ms, 0.549×, and whole compile 267.4 → 207.2 ms, 0.775×**, in the round where `--compile-phases`' built-in parse control held (the round where it moved 1.5–1.9× is discarded on that basis). `RelayRewriteTests` (19 cases: transitive capture through three levels, a write back through one, per-instance loop cells two levels down, a generator and an async body nested in a closure, `this` through an arrow, a named function expression's own name, direct `eval` two levels down, five levels reading and writing every level — **each on both settings of `BROILER_JS_RELAY_REWRITE_ONCE`** — plus the counter invariant). **All five pinned test262 manifests were run against it** at `07adeb44` plus this patch, on linux-x64: **8 710 executed, 8 617 passed, 84 failed, 251 skipped, 9 timed out — identical to the roadmap's §3.4 row manifest by manifest**, and the same *files* rather than the same totals (all 84 need `$262`; the 9 timeouts are lines 7–15 of `test262-failures.txt`). The suite came from a `git fetch --depth 1` of the pinned ref passed through `--suite-root`, because `codeload.github.com` and `api.github.com` both 403 through this session's proxy; the runner's own "Selected 3 160 runnable test(s)" is what says it is the same corpus. No behaviour change and no main-repo fallback needed: the switch defaults on, the probes are emitters nothing else calls, and the counters are touched once per site. |

**`0082` and `0083` are pending against the pinned pointer `07adeb44`.** Each was applied from a
clean checkout of that commit with **`git am --keep-cr`** and the resulting tree diffed against the
branch it was generated from — identical. They touch no file in common, so the order between them
does not matter; apply in number order anyway.

The push to `Broiler-Platform/Broiler.JS` returned **403** from the session's git proxy, so the
pointer is deliberately *not* bumped.

**The four patches this file carried before it (`0078`–`0081`) have all been applied, pushed and
the pointer bumped to `07adeb44`** — matched patch by patch to the submodule log rather than
inferred from this prose. They are `37905aeb` (item 3-7), `14ac195f` (3-8), `cb2e63c6` (3-1) and
`07adeb44` (3-2) in patch order, and the pointer they were pending against, `61c8cc65`, is an
ancestor of the current pin.

The thirty-three patches this file carried before them (`0049`–`0077`, and `0001`–`0048` before
those) have all been applied, pushed and their pointers bumped.
