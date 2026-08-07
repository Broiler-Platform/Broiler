# Phase 5 — RegExp

The gate that overturned the phase, the three allocation fixes, the per-pattern `Compiled` race, and the per-call envelope the race found.

> Part of the [Broiler performance and benchmark roadmap](../performance-roadmap.md).
> The roadmap carries the status tables, the sequencing and the non-goals; this file carries one part of the detail. Every part is listed there.

---

## Phase 5 — RegExp

**Target: RegExp (110×), plus PdfJS and Typescript.** Blocker **B5**.

Deliberately late: it costs one score, measured against Octane's *lowest* reference
baseline. But its value is larger than that score suggests, because the same engine is
on PdfJS's and Typescript's critical path, and the component has its own roadmap at
[`Broiler.JS/Broiler.Regex/docs/roadmap.md`](../../Broiler.JS/Broiler.Regex/docs/roadmap.md).

**Order.** Profile `Matching/Matcher.cs` against the Octane regex corpus **first**, to
separate backtracking *strategy* from per-step *interpretive overhead*. Then compile
the common subset — literal prefixes, character classes, bounded quantifiers — keeping
the interpreter as the fallback.

**Gate:** the corpus is profiled **before** any rewrite. Broiler.JS additionally owns
the integration gate from its own roadmap: route only features the native engine
implements and tests, compare both backends during expansion, move `Exec`, `Split` and
`Replace` to one match-data abstraction, and retire the .NET translator only after the
pinned RegExp corpus is clean.

### The gate is satisfied, and it overturns the phase — **`Matcher.cs` is not on this path**

Profiled with `--regex-profile` (new; `RegexProfileMetrics`), and the first thing the profile
established is that **the engine this phase is written about barely runs**.

**`Broiler.Regex` is not the default engine, by design.** `JSRegExp.Broiler.cs` says so in its
own header — *"JSRegExp keeps the mature .NET translator as the default engine and routes ONLY
gap-feature patterns that Broiler.Regex can fully handle through it"* — and `GapScan` defines
"gap" precisely: an astral or lone-surrogate atom under `u`, a back-reference inside a
look-behind or in Unicode mode, a capturing group inside a look-behind, or a nullable quantifier
that can repeat. **Octane's `regexp.js` contains no look-behind and no `u` flag at all**, so
essentially none of the suite reaches `Matching/Matcher.cs`. B5's sentence — *"`Broiler.Regex`'s
`Matching/Matcher.cs` has no compilation to native code … the same engine sits on PdfJS's and
Typescript's critical path"* — is describing a component those workloads route around.

**What does serve them is `System.Text.RegularExpressions`, built INTERPRETED.**
`JSRegExp.ParseFlags` starts from `RegexOptions.ECMAScript` and the pattern is constructed as
`new Regex(pattern, options)`; **`RegexOptions.Compiled` appears nowhere on the user-regex
path**, though the engine does use it for `Intl` and `DateParser`. So the phase's own plan —
"compile the common subset, keeping the interpreter as the fallback" — would be building a
compiler for a path the benchmark never takes, while the path it *does* take has compilation
available behind one flag.

**Measured, on seven patterns lifted from `regexp.js` itself**, 200 000 matches each, the same
`RegexOptions.ECMAScript` the engine ships against that plus `Compiled`:

| Pattern | Interpreted | Compiled | Speedup | Build (interp → compiled) |
|---|--:|--:|--:|--:|
| `^ba` | 10.19 ms | 4.72 ms | **2.16×** | 1.2 µs → 6.8 µs |
| `,` | 9.53 | 4.67 | **2.04×** | 1.0 → 6.3 |
| `(-[a-z])` | 14.38 | 7.69 | **1.87×** | 2.4 → 13.4 |
| `[+, ]` | 9.48 | 5.01 | **1.89×** | 1.3 → 6.6 |
| `TNQP=([^;]*)` | 16.98 | 8.41 | **2.02×** | 1.6 → 12.1 |
| `[<>]` | 9.27 | 5.01 | **1.85×** | 1.4 → 6.7 |
| **`^[\s\xa0]+\|[\s\xa0]+$`** | 17.95 | **71.54** | **0.25× — four times SLOWER** | 4.2 → 25.9 |

**Six of seven are worth about 2×, and the seventh is a 4× regression** — which is exactly why
this is a measurement and not a flag to set globally. Construction costs 5–6× more compiled, but
in absolute terms 7–26 µs against a pattern Octane builds once and matches hundreds of thousands
of times, so the trade is not close *where it wins*. A per-pattern decision — compile on the
second or third use, the way tiering already reasons about functions — is the shape this wants,
not a blanket option.

**And the largest regex-shaped cost in the engine is not matching at all.** Nine JS-level shapes
over a 20 000-character subject, net of an inert loop:

| Shape | ns/char | B/char |
|---|--:|--:|
| `re.test` miss — literal, class, alternation | 0.13 – 0.35 | ~0.02 |
| `re.exec` hit with eight captures | 0.80 | 2.23 |
| `/a*b/` quantifier walk, fails at every position | 0.95 | 0.02 |
| `String.indexOf` for the same literal *(floor)* | 15.94 | 0.00 |
| **`subject.replace(/[aeiou]/g, 'x')`** | **1 318** | **10 522** |

The miss rows cost *less than `indexOf`*, which is the clearest possible statement that matching
is not where the time goes. `replace` with a global flag is **~3 800× the next row in time and
~4 700× in bytes**: 20 calls allocated **4.21 GB**, i.e. **210 MB per call and ~42 KB per match**
on a 40 KB subject with 5 000 matches. Time scales only mildly superlinearly (2.3× for a doubled
subject), so this is **allocation per match**, not an algorithmic blow-up — a match-result object
built per match, which is the same shape as phase E's quadratic string concatenation and wants
the same treatment.

**So phase 5 is re-specified, and re-ordered against itself:**

1. **Stop allocating per match on the `replace`/`exec` result path — landed, see below.**
   Largest measured cost by three orders of magnitude, and it is the one that reaches PdfJS and
   Typescript — which is what this phase claimed to care about.
2. **Decide `RegexOptions.Compiled` per pattern** — measured further, and **a use count is not
   enough to decide it**. See below.
3. **Only then consider compiling `Broiler.Regex`.** It is correctness-critical for the gap
   cases and it should stay, but no measurement here puts it on a hot path, and B5's ranking of
   it was never checked against the routing.

**RegExp's 110× is therefore not evidence about `Matcher.cs`**, and the score should not be
quoted as if it were until something establishes which engine produced it.

#### Item 1 landed — an Annex B legacy static was copying the subject on every match

The profile said `replace` with a global flag cost **10 522 bytes per subject character**. The
cause is one line, and it is not in either regex engine.

`RegExpBuiltinExec` calls `LegacyRegExpState.Update` on every **successful** match, to keep
Annex B §B.2.4's deprecated statics warm — `RegExp.lastMatch`, `RegExp.leftContext`,
`RegExp.rightContext` and friends. `LeftContext` and `RightContext` **partition the subject
around the match**, and they were built eagerly:

```csharp
LastMatch    = input.Substring(startIndex, endIndex - startIndex);
LeftContext  = input.Substring(0, startIndex);      // O(startIndex)
RightContext = input.Substring(endIndex);           // O(length - endIndex)
```

Together those copy the **entire subject, once per successful match**. And
`RegExp.prototype[@@replace]` is the generic spec path — it calls `exec` once per match — so a
global replace was **quadratic in allocation**: measured at **42 859 bytes per match**, 204 MB
for one call over a 40 KB subject with 5 000 matches.

**The fix is to record the span and slice on read.** Nothing needs those substrings until
somebody reads one, and almost nothing ever does — they are a deprecated compatibility surface.

| | Before | After | |
|---|--:|--:|--:|
| `replace(/[aeiou]/g, 'x')` | 10 522 B/char | **504 B/char** | **0.048x** |
| the same, time | 1 318 ns/char | **397 ns/char** | **0.30x** |
| `exec` with eight captures | 2.23 B/char | **0.23 B/char** | 0.10x |
| `test`, matching | 2.22 B/char | **0.22 B/char** | 0.10x |
| every **miss** row | 0.01 – 0.02 | **unchanged** | — |

The miss rows are the control and they do not move by a byte, which is what identifies the cost
as *per successful match* rather than per scan.

**What remains, decomposed — and the per-character framing was hiding it.** A `--regex-profile`
scaling section now reports bytes **per call** at three subject lengths, because 4 400 bytes on a
20 000-character subject reads as 0.22 B/char whether it scales with the subject or not:

| Operation | 5 000 | 20 000 | 80 000 | Reading |
|---|--:|--:|--:|---|
| `test`, matching | 2 051 | 2 351 | 3 551 | **~1 950 B fixed** + 0.02 B/char |
| `exec`, matching | 1 995 | 2 295 | 3 495 | the same, and the result array is nearly all of it |
| `replace`, **one** match | 22 635 | 82 935 | 324 135 | **~4 B per subject character** |

So `exec` is **not** proportional to the subject — it is a flat ~2 KB per call, which is the
result array plus its `index` / `input` / `groups` properties. And a *single* non-global
`replace` costs four bytes per subject character, which is **two full UTF-16 copies**: the
`StringBuilder`'s chunks and then its `ToString()`.

Two follow-ups, both sized by those rows. **The first has landed** — see below; the second has
not started:

- **The single-match replace should not use a builder at all.** `input[0..pos] + replacement +
  input[end..]` is one `string.Concat` over three spans and one allocation, halving 4 B/char
  to 2. *Pre-sizing the builder was tried first and is worth 0.2%* — .NET's `StringBuilder`
  is a chunk list, not a doubling array, so there was no reallocation waste to remove. The
  change was reverted rather than kept for a rationale that turned out to be wrong.
  **Landed, and the halving is exact — in both builtins that had the pattern.**
- **A global replace retains every result before it builds anything.** §22.2.6.11 collects all
  matches in step 14 and reads their properties in step 16, so 5 000 matches means 5 000 live
  result arrays — 5 000 × ~2 KB, which is exactly the ~10 MB per call still measured. Streaming
  them would change the observable order of `exec` calls against capture reads, so it is only
  available on a fast path where nothing is patched. **Landed — and the estimate was right to
  three digits: 2 033 bytes per match, dead linear.**

**One hazard the change introduced and closed.** Deferring the slice means `Update` publishes a
subject and two indices that must agree; three separate field writes would let a reader on
another thread pair a new subject with the previous match's indices and slice outside it. The
eager version could not do that — its fields were independent, already-built strings, so a torn
read returned something stale but valid. They are now one immutable record published by a single
reference write.

**Verify.** `LegacyRegExpStaticsAllocationTests` asserts the bytes, because nothing about the
*answers* changes and `Issue845RegExpAndWithTests`' twenty existing cases pass either way — on
the build without this, the allocation test reports **204.4 MB (42 859 B/match)** against its
50 MB bound, so it fails by a factor of four. A second test pins that the statics still describe
the last match, so the allocation test cannot be satisfied by simply not recording them.
Repository suite **7 563 tests across 13 projects, 3 failures**, the pre-existing win-x64 host
ones. **test262 unchanged across all four pinned manifests** — 8 220 passed, 84 failed, 9 timed
out, identical manifest by manifest. **Octane 14 of 15 `ok`**, the same set, with Mandreel's
failure record byte-identical to the earlier runs.

> **Octane's scores moved the right way and are not claimed.** Across this session's four
> broiler-only runs RegExp went 131, 126, 132 → **140** and Typescript 2 935, 2 951, 2 998 →
> **3 257**, so both landed above the spread of the three runs that preceded the change — which
> is the direction a per-match subject copy disappearing should push the two suites that use
> regexes most. **One repetition per side cannot separate a change from noise (§3.2)**, and
> these are single runs on a developer workstation, so what is claimed here is the allocation
> figure, which is deterministic and exact. The scores are recorded as corroboration and as a
> reason for 0-6 to look at them.

#### The single-match follow-up landed — the halving is exact

> **Delivered as a patch, not in the pin.** The push to `Broiler-Platform/Broiler.JS` returned
> **403** — the submodule remote was outside that session's GitHub scope — so the change shipped
> as `patches/0059` with the pointer unbumped. **It has since been applied and pushed, and is now
> `962ca06a`, an ancestor of `61c8cc65` (the pin at the time).** Every figure below was measured on a local
> build of the then-pinned `2ebc0c3c` **plus** that patch, with the control built from the same
> tree minus it — so they describe the tree the pin now contains.

`RegExp.prototype[@@replace]` accumulated into a `StringBuilder` whatever the match count. For a
single match the answer is exactly `prefix + replacement + suffix`, so `string.Concat` over three
spans writes it into **one** allocation of the final length, and the builder's two copies — into
its chunk list, then back out through `ToString()` — become one.

Measured with the same `--regex-profile` scaling rows, both sides built from the same tree rather
than compared against the figures recorded above:

| `replace`, one match | 5 000 | 20 000 | 80 000 | Slope |
|---|--:|--:|--:|--:|
| Before | 22 635 | 82 935 | 324 190 | **4.020 B/char** |
| After | 12 483 | 42 783 | 164 030 | **2.020 B/char** |
| | 0.55x | 0.52x | 0.51x | **exactly half** |

**The predicted halving is realized to two decimal places, and that is the load-bearing part.**
The decomposition claimed the 4 B/char was two copies of the subject *and nothing else*; removing
one copy and getting exactly half is what confirms there was no third. The `test` and `exec` rows
are byte-identical on both sides — 2 051 / 2 351 / 3 551 and 1 995 / 2 295 / 3 495 — so they are
the control, and they place the saving on the replace path rather than in the profile.

*(The before row's 80 000 figure is 324 190 here against the 324 135 recorded in the table above.
Both are this build's own control, taken a pointer apart; 55 bytes on 324 KB is 0.02% and changes
no slope. It is written as measured rather than reconciled to the earlier row.)*

**The same assembly was one file over, and it is fixed too.** `String.prototype.replace` with a
**string** `searchValue` never reaches `@@replace` — it is a separate builtin — and it replaces
only the first occurrence, so it is single-match *by construction*. It built its answer from three
appends into a **pre-sized** `StringBuilder`: the same two copies, and the clearest case of why
pre-sizing does not help, since that builder was already sized exactly right. `--regex-profile`
now carries a `replace-one-string` row beside `replace-one` so the two cannot drift apart:

| `replace`, one match, **string** searchValue | 5 000 | 20 000 | 80 000 | Slope |
|---|--:|--:|--:|--:|
| Before | 20 406 | 80 706 | 321 965 | **4.020 B/char** |
| After | 10 326 | 40 626 | 161 877 | **2.020 B/char** |
| | 0.51x | 0.50x | 0.50x | **exactly half** |

Its slope was already identical to the regexp path's to three decimal places before the change,
which is what identifies the two as the same defect rather than two similar ones — and it lands
on the same 2.020 after. **It was found by reading the neighbouring builtin, not by the profile**,
which had no row for it; the row exists now because the fix needed one.

**A global regexp that matches once takes the fast path too.** The gate is `results.Count == 1`,
not the `g` flag: `global` decides how many results were collected, not how they are assembled,
so `'abc'.replace(/b/g, 'X')` gets it. And step 16.p's backwards-position guard cannot apply to a
single result — `nextSourcePosition` is still 0 and `position` is clamped to [0, lengthS], so
`position >= 0` holds — which makes the fast path the loop's behaviour rather than an
approximation of it.

**The per-result work is shared, not duplicated.** Step 16 reads the result's properties in an
order a Proxy can observe (`length` → `0` → `index` → captures → `groups`), and test262
`sm/RegExp/replace-trace` pins it. Both paths call one local function rather than each carrying a
copy, so the order cannot drift between them; it is called directly and never as a delegate, so
the capture is a struct closure and costs no allocation.

**Verify.** `SingleMatchReplaceAllocationTests` asserts the bytes and **fails on the build
without the change** — 95 881 B/call against its 60 000 bound — while every one of its answer
cases passes on *both* builds, which is what identifies them as regression guards rather than
change detectors. They cover the edges the fast path now owns: a match at position 0 and at the
end of the subject, an empty match, an empty replacement, the global-matching-once case, every
`$` substitution form, functional replacers, surrogate pairs, an ill-behaving `exec` reporting an
index past the subject, the Annex B statics, and the property read order — plus the same edges
again through the string-`searchValue` builtin.

**test262 is unchanged across all four pinned manifests — 8 313 executed, 8 220 passed, 84
failed, 44 skipped, 9 timed out, identical manifest by manifest** to §3.4's recorded run, at
suite ref `ccaac100`. And because the four pinned manifests contain no `replace` coverage at all,
**the paths this change actually touches were run separately, control against change**:
`RegExp/prototype/Symbol.replace`, `String/prototype/replace`, `replaceAll`, `RegExp/prototype/exec`,
`Symbol.split`, `Symbol.match`, `annexB/built-ins/RegExp` and `staging/sm/RegExp` — **499 tests,
484 passed, 13 failed, 2 timed out, and the failing set is the same file for file on both
builds.** All 13 are cross-realm cases needing `$262.createRealm`, which the raw script host does
not provide; **not one failure is in a `replace` directory.** Repository suite
**7 604 tests across 13 projects, 0 failures** — 41 of them new here.

#### The retained-result-list follow-up landed — 2 033 bytes a match, and the guard is the change

> **In the pin.** Shipped as `patches/0060` for the same 403 as the section above; since applied
> and pushed, and it is now **`6f56d24f`**, an ancestor of `61c8cc65` (the pin at the time). Figures below
> were measured on a local build of the then-pinned `2ebc0c3c` plus it and `0059`.

`RegExp.prototype[@@replace]` collects every match in step 14 and only reads their properties in
step 16, so a global replace held one result array per match live before it assembled anything.
**Measured with the subject held fixed and only the match count varying** — which is the
discriminator the earlier scaling rows could not be, since they vary the subject and hold the
match count at one:

| Global replace, 40 000-char subject | 500 matches | 2 500 | 5 000 | Slope |
|---|--:|--:|--:|--:|
| Before | 1 181 612 | 5 247 695 | 10 329 158 | **2 032.8 B/match** |
| After | 409 588 | 1 363 919 | 2 561 870 | **478.3 B/match** |
| | 0.35x | 0.26x | 0.25x | **0.235x** |

Dead linear on both sides — 2 033.0 and 2 032.6 across the two independent intervals before the
change — so the retained list was the whole of it, and the previous section's "5 000 × ~2 KB ≈
10 MB" estimate was right to three digits rather than approximately. What is left at 478 B/match
is the match data itself: the `RegexMatchData`, its capture array and the matched string.

**The optimization is four lines; the guard is the item.** Appending each replacement as it is
produced, instead of collecting first, is trivial. Establishing that nobody can *watch* the
results being skipped is the entire design, and it needs three conditions at once:

| Condition | Why, and what breaks without it |
|---|---|
| The receiver's `exec` **is** the pristine `%RegExp.prototype.exec%` | Every result is then a fresh array this function is the only holder of. A patched `exec` — own property or on the prototype — must run, and its results are the user's |
| The replacement is a **string**, not a function | A functional replacer is user code running *between* matches |
| That string contains **no `$`** | `$&`, `` $` ``, `$'`, `$n` and `$<name>` all read back through the result object |

**Two of those three are not obvious from the item's own description, and one is a real trap.**
The item says streaming "would change the observable order of `exec` calls against capture reads",
which is true but understates the functional-replacer case: because the spec collects *all*
matches before calling *any* replacer, the final failing `exec` has already reset `lastIndex` to
**0** by the time user code first runs. A streamed replacer would instead see `lastIndex` sitting
mid-subject, at a different value for every call. That is not a reordering, it is a different
observable value, and `AFunctionalReplacerIsNotStreamed` pins it at `0,0`.

**Identity against a pristine capture is the only sound form of the `exec` test.** `%RegExp.prototype.exec%`
is captured into `JSContext.IntrinsicRegExpExec` at realm init, before any user code can run —
the same mechanism `IntrinsicArrayValues` and `IntrinsicPromisePrototype` already use. Reading
`RegExp.prototype.exec` later and comparing it to itself would be circular: by then it may
already be the patched one.

**Matching still goes through one shared code path.** `Exec` is split into `ExecMatch` — the
`lastIndex` read and write, the match, the sticky re-check and the Annex B statics — and
`BuildExecResult`, which is the part the fast path skips. Both callers use `ExecMatch`, so
`lastIndex` progression, engine routing and the statics cannot drift between the two paths,
because they are the same code rather than the same intention written twice. This is the same
device 0059 used for step 16's property read order, and for the same reason.

**Verify.** `GlobalReplaceStreamingAllocationTests` asserts the bytes and **fails on the build
without the change** — 2 610 B/match against its 1 000 bound — while all 22 guard cases pass on
*both* builds, which is what makes them regression guards rather than change detectors. They
cover each exclusion (patched own `exec`, patched prototype `exec`, an `exec` returning null, a
functional replacer, and every `$` form), the empty-match advance under `/u` and without it
(asserted as code units, because a C# literal for a lone surrogate is its own trap), sticky with
global, `lastIndex` after the call, and the Annex B statics.

Repository suite **7 627 tests across 13 projects, 0 failures**. **test262 unchanged across all
four pinned manifests — 8 313 executed, 8 220 passed, 84 failed, 44 skipped, 9 timed out,
identical manifest by manifest**, at suite ref `ccaac100`. The replace-path manifest was re-run
too: **499 tests, 484 passed, 13 failed, 2 timed out, and the failing set is identical file for
file to the *pre-0059* control** — so both of this phase's follow-ups together move no test262
file in either direction.


#### Item 2 measured — and the obvious policy is the wrong one

The single-run table above had one pattern losing badly under `Compiled`. Repeated three times
it is **stable, not noise**: `/^[\s ]+|[\s ]+$/` — an ordinary *trim* — measures
**0.236, 0.225, 0.237**, consistently about **4.3× slower compiled**, while the other six sit
between 1.7× and 2.3× faster.

That kills "compile after N uses", which is the policy this item was about to specify: a trim is
exactly the kind of pattern a program runs hundreds of thousands of times, so a use counter would
find it first and make it four times worse.

**So the loss was characterized rather than guessed at**, with four probes decomposing the
pattern (they are kept in the emitter, since the next attempt needs them):

| Probe | Speedup | |
|---|--:|---|
| `^[\s ]+` — anchor + class quantifier | 0.366, 0.365 | **loss** |
| `[\s ]+$` — the other anchor | 0.464, 0.419 | **loss** |
| `[\s ]+\|zzz` — **same class, no anchor** | 2.758, 2.938 | big win |
| `^a+\|b+$` — **anchored alternation, literals** | 3.425, 2.765 | big win |

**It is neither alternation nor anchoring**, which were the two obvious readings — an anchored
alternation of literal quantifiers is one of the *best* rows in the set. What loses is
specifically an **anchored character-class quantifier**, and the `trim` pattern is that shape
twice over.

**No policy is shipped, on purpose.** The rule above is drawn from eleven patterns on one
runtime, and turning it into "compile unless the pattern begins with an anchored class
quantifier" would be exactly the kind of heuristic §3.5 warns about — a branch described from
its intent rather than traced with real numbers. What the next attempt needs, in order: the same
comparison over a corpus far wider than Octane's, an explanation of *why* the compiled path
loses that shape (it is .NET's codegen, not this engine's), and only then a predicate. A
per-pattern decision made by measuring both forms once on the real subject — the way tiering
already reasons about functions — is the design most likely to survive that, because it needs no
predicate at all.

#### Item 2 built — the race, and it answers the item by refuting the phase's remaining premise

The section above closed with a design rather than a rule: *"A per-pattern decision made by
measuring both forms once on the real subject — the way tiering already reasons about functions —
is the design most likely to survive that, because it needs no predicate at all."* **That is now
built, tested and measured**, and what it produced is a *negative* result with a much larger
positive one behind it.

**Where.** `Broiler.JavaScript.BuiltIns` — `RegExp/RegexTiering.cs` (the policy),
`RegExp/RegexTieringDiagnostics.cs` (the counters and the per-race rows), and three lines in
`JSRegExp`: a countdown field, one test in `RunMatch`, and an `ArmTiering()` call after every
assignment to the matcher. **Off unless `BROILER_JS_REGEX_TIERING=1`.**

**The mechanism, in one paragraph.** An instance counts its own matches; at the thousandth,
the engine builds `RegexOptions.Compiled` for the same pattern and option set, times both arms
ABBA-interleaved on the subject in hand under a 4 ms budget, and keeps the compiled one only if
it wins by **1.15×**. The verdict is cached by (pattern, options) so sibling instances of the
same literal inherit it. It never re-decides, and it never touches a pattern routed to
`Broiler.Regex`. **A tie keeps the interpreted arm**, because the compiled one costs a
`DynamicMethod` that is never reclaimed.

*The reason a timing-driven decision is admissible in a language runtime at all is that
`RegexOptions.Compiled` changes code generation and nothing else* — the two `Regex` objects are
built from one pattern string and one option set and match identically, so the race chooses
between two implementations of one function rather than between two behaviours.
`RegexTieringTests` asserts exactly that: **15 cases, each run on both settings and required to
agree** — captures and their order, named groups, `lastIndex` across a global exec loop, sticky
in both directions, a backreference, `replace` with a function, `split`, the Annex B statics, a
gap-feature pattern that must never race, and `RegExp.prototype.compile` replacing a matcher
that has already gone hot. All 15 pass on the unmodified engine too; they are a guard, not a fit.

**Repository suite 8 219 tests across 13 projects, 0 failures. test262 unchanged across all five
pinned manifests on the arm where the mechanism fires** (`BROILER_JS_REGEX_TIERING=1`, at the pin
plus this patch): **8 710 executed, 8 617 passed, 84 failed, 251 skipped, 9 timed out — identical
to §3.4's row, manifest by manifest**, and identical as *files* rather than as totals: every one
of the 84 failures needs `$262`, and the 9 timeouts are nine of nine the integer-limit cases
already tracked. `properties-proxy` is the manifest that matters here, since it is thick with
`RegExp.prototype` receiver and descriptor cases and a promotion that altered a capture layout
would surface there rather than in a benchmark.

#### What it does on the corpus: fourteen suites of fifteen never reach it

`--specializing-tier … counters` now reports `regexPatternsBuilt`, `regexRaces`,
`regexRacesPromoted`, `regexVerdictsReused` and a per-race detail row. Over **all fifteen
suites**:

| Suite | patterns built | races | promoted |
|---|--:|--:|--:|
| **RegExp** | **3 069** | **6** | **5** |
| Typescript | 615 | 0 | 0 |
| CodeLoad | 432 | 0 | 0 |
| PdfJS | 273 | 0 | 0 |
| *the other eleven* | **0** | 0 | 0 |

**Eleven of Octane's fifteen suites do not build a regular expression at all**, and of the four
that do, only one has a pattern that matches a thousand times through a single object. *So the
mechanism's entire addressable surface on this corpus is one benchmark.*

#### The six races, and the pattern the whole "no policy" decision rested on promotes at 5.27×

The per-race rows are the reason the aggregate cannot be read on its own. Patterns are shown as
the matcher holds them — after `JSRegExp`'s ECMAScript translations, which is why `\s` appears
expanded and the URL pattern's groups are renamed:

| Octane source | translated | subject | interpreted | compiled | × | verdict |
|---|---|--:|--:|--:|--:|---|
| `re0` `/^ba/` | `^ba` | 5 | 0.0076 ms | 0.0013 | **5.85×** | promoted |
| `re1` the URL parser | 11 renamed groups | 33 | 0.0390 | 0.0084 | **4.64×** | promoted |
| **`re2` `/^\s*\|\s*$/g`** | `^[\s…]*\|[\s…]*$` | 22 | 0.0601 | 0.0114 | **5.27×** | **promoted** |
| `re8` `/=/` | `=` | 16 | 0.0100 | 0.0067 | 1.49× | promoted |
| `re14` `/\s+/g` | `[\s…]+` | 14 | 0.0100 | 0.0088 | 1.14× | **refused** |
| `/\b\w+\b/g` (a literal in a loop) | `\b\w+\b` | 4 081 | 0.0080 | 0.0051 | 1.57× | promoted |

***`re2` is the shape this item was told not to compile.*** An anchored character-class
quantifier, alternated with a second one — the same structure as the `trim` the section above
measured at **4.3× against**, and once both are translated they differ only in `*` for `+` (the
engine expands `\s` to include `\xA0` either way). On Octane's own 22-character subject the
compiled form is **5.27× faster**, and it is the largest single win in the set. **A predicate
drawn from the earlier table would have refused it.**

**The last row also answers the objection that a threshold of 1 000 is unreachable for a regex
literal.** `/\b\w+\b/g` is written inside a loop, so the engine builds a fresh `JSRegExp` on
every evaluation — and it still races, because one **global** `replace` drives a thousand matches
through the single object that evaluation created. *A per-instance countdown reaches a global
pattern in one call and a non-global one never; that asymmetry is real and is why the verdict
cache is keyed by pattern rather than held on the object.*

**The one refusal is the adoption margin working.** `[\s…]+` measures 1.14× — just under the
1.15 threshold — and an earlier run of the same census promoted it, making the count 6 of 6
rather than 5 of 6. *That is the only decision that has ever moved between runs, and it moves
only where the two arms are within 14% of each other, which is the case where being wrong costs
almost nothing.* A policy whose unstable region is exactly its indifferent region is behaving
correctly; one whose unstable region included `re2` would not be.

#### And the anomaly the "no policy" decision rested on does not reproduce

`--regex-profile`'s `netEngine` table is unchanged code, re-run here, 200 000 matches per arm:

| Pattern | recorded above | re-taken here |
|---|--:|--:|
| `^ba` | 2.16× | 1.69× |
| `,` | 2.04× | 1.59× |
| **`^[\s\xa0]+\|[\s\xa0]+$`** — trim | **0.25× (4.3× against)** | **1.23× — for** |
| `(-[a-z])` | 1.87× | 1.88× |
| `[+, ]` | 1.89× | 1.53× |
| `TNQP=([^;]*)` | 2.02× | 1.73× |
| `[<>]` | 1.85× | 1.63× |
| **`^[\s\xa0]+`** — anchored class quantifier | **0.366×** | **1.50×** |
| **`[\s\xa0]+$`** — the other anchor | **0.464×** | **1.17×** |
| `[\s\xa0]+\|zzz` | 2.76× | 1.34× |
| `^a+\|b+$` | 3.43× | 2.75× |

**All three of the losing rows have changed sign, and the wins are uniformly smaller.** The
earlier reading was careful to say the loss *"is .NET's codegen, not this engine's"* and to name
"an explanation of why the compiled path loses that shape" as a precondition for any predicate.
This is that sentence collecting: nothing in Broiler changed between the two readings, so what
moved is the host — and **a rule compiled into the engine from the first table would now be
wrong on every pattern it named**. *The case for a race over a predicate is no longer an argument
about generalisation; it is a measurement of a predicate's premises expiring.*

#### And the race is worth nothing measurable on the suite — 1.010× on 3 of 6 pairs

`--specializing-tier … timing "RegExp"`, one process per arm, six ABBA-interleaved pairs:

| | median | samples |
|---|--:|---|
| off (shipping) | **2 278 ms** | 2 314, 2 385, 2 288, 2 196, 2 268, 2 122 |
| on | **2 255 ms** | 2 223, 2 711, 2 274, 2 244, 2 266, 2 155 |
| | **1.010×** | **3 of 6 pairs won by `on`** |

**Three of six is the definition of no separation** (§3.5: *"the sample count has to grow until
the arms separate by rank, not by median"*), and unlike most negative results in this document
this one did not need more samples — the next section says why the arms cannot separate, and it
is not a property of the race.

#### The measurement that explains all of it: the matcher is 5% of a regex call

`--regex-call-envelope` (new) runs the identical work at the identical iteration count twice —
once through the engine, once through `System.Text.RegularExpressions` directly — with the right
control on each side: `IsMatch` for `test` (it allocates nothing) and `Match` for `exec` (it
materialises the groups). 200 000 iterations, medians of five, allocation exact:

| Site (subject) | `IsMatch` | `Match` | `re.test` | `re.exec` | `s.search(re)` | control |
|---|--:|--:|--:|--:|--:|--:|
| `^ba` (30 ch) | 112.8 ns / **0 B** | 226.4 / 208 | **2 429.8 / 2 431** | 2 179.7 / 2 375 | 2 780.3 / 2 431 | 125.1 / 32 |
| `(-[a-z])` (53 ch) | 134.9 / 0 | 234.8 / 248 | 2 943.8 / 2 687 | 2 599.4 / 2 631 | 3 461.4 / 2 687 | 119.0 / 32 |
| trim (50 ch) | 195.7 / 0 | 338.3 / 208 | 3 015.3 / 2 447 | 2 662.8 / 2 391 | 3 113.2 / 2 447 | 124.3 / 32 |
| **`^ba` (564 ch)** | 78.2 / 0 | 185.2 / 208 | **2 466.6 / 2 431** | 2 360.4 / 2 375 | 2 714.6 / 2 431 | 116.2 / 32 |

> ***The matcher is 4.6–6.5% of what `re.test` costs, and 8.7–9.4% of what `re.exec`
> allocates.*** A change that made matching *infinitely* fast would move a `test` loop by about
> a twentieth.

**The last row is the discriminator and it rules out the obvious explanation.** The same
anchored pattern against a subject **18.8× longer** decides at position 0 either way, and the
engine rows do not move: `2 431 → 2 431 bytes, exactly`, and 2 429.8 → 2 466.6 ns. *So the ~2.4 KB
is not a copy of the subject — it is a fixed cost per call*, which is a different item from the
one `0059` and `0060` fixed on `replace` and needs a different fix.

**Three more readings the table gives for free:**

- **`test` costs more than `exec`** — +250 ns and +56 B — though the spec defines it as
  `RegExpExec(R, S) ≠ null` and nothing else. It is not an inference: `Test` calls `RegExpExec`,
  which calls `Exec`, which calls `BuildExecResult`. So a `test` builds the array, the matched
  string, `index`, `input` and `groups`, and discards all of it on the next line.
- **`search` is the dearest of the three**, at 2 714–3 461 ns, despite returning an integer.
- **The engine adds 2 167–2 383 B to every successful match**, against 208–248 B for the
  `Match` object it is wrapping.

#### Status, and what phase 5 is now

**Item 2 is answered and the mechanism is shipped switchable, default off.** It is correct on
both settings, its verdicts are stable where they matter and indifferent where they are not, and
it is worth **1.010× on 3 of 6 pairs** on the only Octane suite it reaches. It is kept rather
than reverted for the reason `0111`'s refused fix was not kept and this one is: *the code is
there, tested on both settings, and the moment the envelope below shrinks the matcher's share
rises and the same race is worth re-taking on a build where it can pay.* Turning it on today
would be buying a `DynamicMethod` per hot pattern for a number this container cannot resolve.

**What phase 5 should do next is not the matcher.** Every item in this phase so far — the
profile, the two `replace` copies, the retained result list, and now the compile decision — has
been aimed at matching or at one builtin's assembly. The envelope table says the remaining prize
is **the fixed 2.4 µs and 2.4 KB every JS regex operation pays regardless of pattern, subject or
outcome**, of which the matcher is a twentieth. **That is a new item and it is the largest one
phase 5 has ever had**; it is deliberately *not* specified further here, because the honest next
move is to decompose that 2.4 KB by allocation site rather than to guess at it from the three
builtins' shapes — and the instrument for that is `GC.GetAllocatedBytesForCurrentThread` around
named regions of `ExecMatch` and `BuildExecResult`, not a profiler.

*The item that was going to ship a heuristic instead measured the heuristic's premises expiring,
and the negative result it produced is what found the phase's real target.*

> **The RegExp checksum failure 2-8 recorded did not reproduce.** All three Octane runs this
> session scored it (131, 126, 132) rather than failing `Error: Wrong checksum.`. Left as an
> observation, not a claim: those are single runs on a different platform from the one that
> recorded the failure, and nothing here was aimed at it.

---
