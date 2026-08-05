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
| `0087-js-numeric-tree-order` | `Broiler.JS` | **Item 3-1's order-preserving guard placement, and it is the largest single result phase 3 has produced.** `0084` removed 12.2% of the corpus's boxes against a census ceiling of 86.6% and never said which of its six eligibility conditions refused the rest — a numerator with no denominator, which by §3.5 is a claim about the instrument. A refusal waterfall on item 3-6's terms (attribute each candidate to the **first** condition it fails) says **862 of 5 396 candidate arithmetic nodes specialize, 16.0%**, and that the two rules turning down the rest are **one** finding: `+` is left-associative, so `a[0]+a[1]+a[2]+a[3]` refuses at the root as **order-unsafe** (1 762), refuses again at each left child, and its bottom node is then a lone operator with **no saving to make** (2 718) — *a chain of k operators produces k−1 order-unsafe rows and one no-saving row and specializes nothing.* The sub-census then said the rule is not refusing what this phase assumed: the blocking leaf is a **property read 1 028 times and a computed element read 34 — 1.9%** — so after six items written around array-resident data, the leaf that blocks the order rule is an **object field**, 984 of them Box2D's. **The fix is that nothing required the leaves to move.** The hoisting form is bounded by the fact that it hoists: every leaf goes into a temporary ahead of one combined test, so a leaf crossing a coercion has to be pure. Emitting each leaf at its own postorder position and putting the test **where the coercion it stands in for would have run** preserves the reference order exactly, and the purity rule then has nothing left to protect — the same soundness argument `0084` makes, read from the other end. Each internal node carries a `bool`, a raw `double` and a `JSValue`, so a failure part-way up boxes the accumulated double **once**, at the node that failed, and the rest runs generically; the hoisting form has to fall back to the whole generic tree. **Measured**, one build, `BROILER_JS_NUMERIC_TREE_ORDER` the only difference: generic arithmetic invocations **53 353 957 → 6 626 052 (0.124×)** and boxes **67 795 858 → 31 162 330 — 36 633 528 removed, 54.0% of everything the corpus allocates**, against 12.2% for `0084`, 9.4% for `0086` and **0.36% for the five locals items combined**; from the pre-`0084` baseline the corpus is **85 255 034 → 31 162 330, 0.366×**. Per suite Crypto **0.402×**, Box2D 0.490×, NavierStokes 0.518×, RayTrace 0.585×. `OrderUnsafe` goes **1 762 → 0** and `NoSavingToMake` **2 718 → 1 181** *without that rule being touched*, which is the chain-residue prediction coming out. **The leaf cap had to be re-measured too, and that is `0084`'s own mistake avoided rather than repeated**: `MaximumSpeculativeLeaves` was 8 and had **never fired** on the corpus, because the order rule refused those trees first; the ordered form accepts whole chains and at 8 turned 85 of them down (80 Box2D's), so at 16 the corpus loses a further **664 338 boxes, 2.1%** — while the *tree count falls*, Box2D 1 109 → 1 090, because a longer chain absorbs sub-trees that were separately specialized. **Wall clock, ABBA-interleaved at process granularity, six pairs, counters off: driver 0.969× on six of six** (0.937–0.999), **NavierStokes 0.834×** (0.793–0.899) and **Crypto 0.893×** (0.866–0.926) **both on six of six**, against the corpus's own controls — Richards and EarleyBoyer remove **exactly zero** boxes and read **1.002× and 0.999×**. ***`0086`'s standing lesson predicts the row that looks wrong***: Box2D removes **51% of its own boxes** and reads 1.003×, because that is 861 000 a second against NavierStokes' 6 500 000, and the two suites that move are exactly the two above ~6 M/s. *54.0% of the allocation buys 3.1% of the time* — with `0084`'s 12.2% → 1.9%, the third reading of the constant that should size the rest of the phase. **The census re-taken on the far side then hands the item back its original premise**: the compiler's boxing conversion is now **47.4%** of all boxing requests (20.6 M → 24.6 M while everything else collapsed) and the `++`/`--` step 33.2%, so **80.6% of what the corpus still boxes is a raw double crossing into a `JSValue` slot or element** — the root-box hypothesis `0085` correctly falsified is true now, and this patch is what made it true. `NumericTreeOrderTests` — 11 fixtures, **every value case on both settings of the switch**: left-leaning chains of elements and of fields, a `valueOf` that mutates a later leaf of a three-node tree, **a throwing coercion that must beat a later leaf that would also throw** (the sharpest one, because both arms throw and only the *message* says whether the order held), four getters logging that every leaf is read once and left to right, a failure half-way up that must leave the rest generic with `valueOf` run exactly once, a String defeating the guard mid-chain, BigInt mixing from the middle of a chain, NaN/infinities/−0 carried across several nodes as raw doubles, ToInt32 wrapping at every node, and a thousand-iteration element kernel — plus **three counter assertions**, because all eleven also pass when nothing specializes. **This patch also rewrites one of `0084`'s own fixtures, and that is the point rather than an inconvenience**: `ATreeWhoseOrderCannotBePreservedIsRefused` asserted the refusal this removes, so it failed the moment the ordered emission landed — the second time in three items that an eligibility fixture has caught its own successor. It is now a Theory on both settings asserting the invariant: **the answer is 25 either way**, and only which form computes it moves (one guarded leaf on the hoisting arm, two on the ordered one). Full repository suite **8 063 tests, 0 failures across 14 assemblies**. **All five pinned test262 manifests run on both arms at `cca39b4d`, linux-x64, `--max-workers 4`**: the shipping arm is **identical to §3.4's row manifest by manifest — 8 710 / 8 617 / 84 / 251 / 9** — and four of five agree **file for file** between the arms; on `arrays` the control arm put `built-ins/Array/prototype/toReversed/length-exceeding-array-length-limit.js` in `failed` rather than `timedOut` (18/8 against 17/9), a test already tracked in `test262-failures.txt`, **with the set of 26 non-passing files the same on both** — recorded rather than rounded off, because 84 against 85 would otherwise read as a regression. No main-repo fallback is needed: the switch defaults on, `BROILER_JS_NUMERIC_TREE_ORDER=0` restores the hoisting form exactly and is the bisection, and the refusal counters are touched once per compiled site rather than per call. **Applies directly to `cca39b4d`**; verified with `git am --keep-cr` from a clean checkout of the pin, and the resulting tree diffed against the branch it was generated from — identical. |

**`0082`–`0086` have all been applied, pushed and the pointer bumped to `cca39b4d`.** They were
matched **patch by patch to the submodule log rather than inferred from this prose**: each patch's
`Subject` resolved to a commit, that commit's own `format-patch` output was diffed against the
patch file — identical once the `From <sha>` line, the blob `index` lines and the trailing git
version are set aside, which is the whole of the difference on all five — and the pointer they were
pending against, `07adeb44`, confirmed an ancestor of the new pin. In patch order they are
`0aa8a558` (item 1-1's remaining half), `9e5b57d3` (3-1's operand census), `0dda32b2` (3-1's
guarded numeric tree), `23fc8fb9` (3-1's boxing-source census) and `cca39b4d` (3-1's `ToNumeric`
reuse).

So every figure recorded for those five now **describes the pinned pointer directly**, rather than
a local build of `07adeb44` plus a patch series applied in order, which is what their sections
previously had to say.

The thirty-seven patches this file carried before them (`0049`–`0081`, and `0001`–`0048` before
those) have all been applied, pushed and their pointers bumped.

**`0087` is pending against `cca39b4d`**, on the usual terms: the push to
`Broiler-Platform/Broiler.JS` returned **403** from the session's git proxy, so the pointer is
deliberately *not* bumped and every figure in its section was measured on a local build of the pin
plus that patch. It is independent of everything cleared above and applies directly to the pin.
